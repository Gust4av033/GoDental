using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GoDental.Application.DTOs;
using GoDental.Infrastructure.Online;
namespace GoDental.Tests.Common;
public sealed class MetaOnlineTests
{
    private static readonly MetaCloudAccount Account=new("v99.0","123456789","987654321"); // Versión sintética: no certifica una versión pública vigente.
    private static readonly DateTimeOffset Now=new(2026,10,23,12,0,0,TimeSpan.Zero);
    private static readonly byte[] AppSecret=RandomNumberGenerator.GetBytes(32);
    private static byte[] Notification(string account="987654321",string phone="123456789",string? body=null)=>Encoding.UTF8.GetBytes("{\"object\":\"whatsapp_business_account\",\"entry\":[{\"id\":\""+account+"\",\"changes\":[{\"field\":\"messages\",\"value\":{\"messaging_product\":\"whatsapp\",\"metadata\":{\"phone_number_id\":\""+phone+"\"},\"messages\":["+(body??Message())+"]}}]}]}");
    private static string Message(string text="Información pública solicitada")=>JsonSerializer.Serialize(new{id="wamid.synthetic",from="50370000000",type="text",timestamp=Now.ToUnixTimeSeconds().ToString(),text=new{body=text}});
    private static string Signature(byte[] bytes)=>"sha256="+Convert.ToHexString(HMACSHA256.HashData(AppSecret,bytes)).ToLowerInvariant();
    [Fact]public void SignedNotificationPinsAccountAndDoesNotInferCustomerOrConsent()
    {
        var bytes=Notification();var result=MetaWebhookVerifier.Verify(bytes,Signature(bytes),AppSecret,Account,Now);Assert.True(result.IsSuccess);var message=Assert.Single(result.Value!.Messages);Assert.Equal("+50370000000",message.Sender);Assert.Equal(Now,message.Timestamp);Assert.Empty(result.Value.Statuses);
        Assert.False(MetaWebhookVerifier.Verify(bytes,Signature(bytes),AppSecret,Account with{PhoneNumberId="1"},Now).IsSuccess);Assert.False(MetaWebhookVerifier.Verify(bytes,Signature(bytes),AppSecret,Account with{BusinessAccountId="1"},Now).IsSuccess);
    }
    [Fact]public void SignatureUsesOriginalBytesAndRunsBeforeJsonParsing()
    {
        var bytes=Notification();var signature=Signature(bytes);var modified=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes)+" ");Assert.Equal("META_SIGNATURE",MetaWebhookVerifier.Verify(modified,signature,AppSecret,Account,Now).ErrorCode);
        var malformed=Encoding.UTF8.GetBytes("not-json");Assert.Equal("META_SIGNATURE",MetaWebhookVerifier.Verify(malformed,signature,AppSecret,Account,Now).ErrorCode);Assert.Equal("META_PAYLOAD",MetaWebhookVerifier.Verify(malformed,Signature(malformed),AppSecret,Account,Now).ErrorCode);
    }
    [Theory][InlineData(null)][InlineData("")][InlineData("sha1=abc")][InlineData("sha256=nothex")]
    public void MissingOrMalformedSignaturesAreRejected(string? signature)=>Assert.False(MetaWebhookVerifier.Verify(Notification(),signature,AppSecret,Account,Now).IsSuccess);
    [Fact]public void RepeatedMessageDeduplicatesAndConflictingMessageFailsAtomically()
    {
        var bytes=Notification(body:Message()+","+Message());Assert.Single(MetaWebhookVerifier.Verify(bytes,Signature(bytes),AppSecret,Account,Now).Value!.Messages);
        bytes=Notification(body:Message()+","+Message("Otra respuesta"));Assert.Equal("META_CONFLICT",MetaWebhookVerifier.Verify(bytes,Signature(bytes),AppSecret,Account,Now).ErrorCode);
    }
    [Fact]public void UnsupportedMediaDoesNotDownloadOrExposeItsUrl()
    {
        var raw=JsonSerializer.Serialize(new{id="wamid.media",from="50370000000",type="image",timestamp=Now.ToUnixTimeSeconds().ToString(),image=new{link="https://127.0.0.1/private",id="opaque"}});var bytes=Notification(body:raw);var message=Assert.Single(MetaWebhookVerifier.Verify(bytes,Signature(bytes),AppSecret,Account,Now).Value!.Messages);Assert.Null(message.Text);Assert.Equal("image",message.Type);
    }
    [Fact]public void DuplicatePropertiesFutureTimestampAndOversizeFailBeforeBusinessEffects()
    {
        var bytes=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Notification()).Replace("\"object\":","\"object\":\"another\",\"object\":"));Assert.False(MetaWebhookVerifier.Verify(bytes,Signature(bytes),AppSecret,Account,Now).IsSuccess);
        bytes=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Notification()).Replace(Now.ToUnixTimeSeconds().ToString(),Now.AddHours(1).ToUnixTimeSeconds().ToString()));Assert.False(MetaWebhookVerifier.Verify(bytes,Signature(bytes),AppSecret,Account,Now).IsSuccess);
        bytes=new byte[MetaWebhookVerifier.MaxBodyBytes+1];Assert.False(MetaWebhookVerifier.Verify(bytes,Signature(bytes),AppSecret,Account,Now).IsSuccess);
    }
    [Fact]public void SubscriptionRequiresCorrectTokenAndBoundedChallenge()
    {var token=new string('k',32);Assert.True(MetaWebhookVerifier.VerifySubscription("subscribe",token,token,"12345"));Assert.False(MetaWebhookVerifier.VerifySubscription("subscribe",new string('m',32),token,"12345"));Assert.False(MetaWebhookVerifier.VerifySubscription("subscribe",token,token,new string('1',513)));}
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler
    {public int Calls;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct){Calls++;return send(r);}}
    private static MetaTemplateMessage Template()=>new(Guid.NewGuid(),"50370000000","presentacion","es_SV",["Negocio de ejemplo"]);
    private static string Token=>new('t',40); // Sintético; no credencial del negocio.
    [Fact]public async Task OfficialTemplateUsesPinnedEndpointAndBearerOutsideUrl()
    {
        var request=Template();var handler=new Handler(async r=>{Assert.Equal("https://graph.facebook.com/v99.0/123456789/messages",r.RequestUri!.AbsoluteUri);Assert.Equal(Token,r.Headers.Authorization!.Parameter);Assert.DoesNotContain(Token,r.RequestUri.AbsoluteUri);using var json=JsonDocument.Parse(await r.Content!.ReadAsStringAsync());Assert.Equal("template",json.RootElement.GetProperty("type").GetString());Assert.Equal("presentacion",json.RootElement.GetProperty("template").GetProperty("name").GetString());Assert.DoesNotContain(Token,json.RootElement.GetRawText());return new(HttpStatusCode.OK){Content=new StringContent("{\"messaging_product\":\"whatsapp\",\"contacts\":[{\"wa_id\":\"50370000000\"}],\"messages\":[{\"id\":\"wamid.accepted\"}]}")};});
        using var transport=new MetaCloudTemplateTransport(new HttpClient(handler));var result=await transport.SendAsync(Account,request,Token);Assert.Equal(MetaDeliveryOutcome.Accepted,result.Outcome);Assert.Equal(request.RequestId,result.RequestId);Assert.Equal("wamid.accepted",result.ExternalMessageId);Assert.Equal(1,handler.Calls);
    }
    [Theory][InlineData("../v99.0")][InlineData("v99.0?token=x")][InlineData("https://evil.example/")]
    public async Task ConfigurationCannotRedirectCredential(string version)
    {var handler=new Handler(_=>throw new InvalidOperationException("No debe llamar red"));using var t=new MetaCloudTemplateTransport(new HttpClient(handler));var e=await Assert.ThrowsAsync<ArgumentException>(()=>t.SendAsync(Account with{ApiVersion=version},Template(),Token));Assert.Equal(0,handler.Calls);Assert.DoesNotContain(Token,e.Message);}
    [Fact]public async Task NetworkLossDoesNotAutomaticallyRetryAndRemainsUncertain()
    {var handler=new Handler(_=>throw new HttpRequestException("Datos del proveedor y secretos no deben salir"));using var t=new MetaCloudTemplateTransport(new HttpClient(handler));var result=await t.SendAsync(Account,Template(),Token);Assert.Equal(MetaDeliveryOutcome.Uncertain,result.Outcome);Assert.Null(result.ExternalMessageId);Assert.Equal(1,handler.Calls);}
    [Theory][InlineData(HttpStatusCode.OK,"not-json",MetaDeliveryOutcome.Uncertain)][InlineData(HttpStatusCode.ServiceUnavailable,"{\"error\":{\"code\":1}}",MetaDeliveryOutcome.Uncertain)][InlineData(HttpStatusCode.Redirect,"",MetaDeliveryOutcome.Uncertain)][InlineData(HttpStatusCode.BadRequest,"{\"error\":{\"code\":100,\"message\":\"private debug\"}}",MetaDeliveryOutcome.Rejected)]
    public async Task OnlyVerifiedProviderAcceptanceConfirmsAndErrorsExposeCodesOnly(HttpStatusCode code,string body,MetaDeliveryOutcome expected)
    {var handler=new Handler(_=>Task.FromResult(new HttpResponseMessage(code){Content=new StringContent(body)}));using var t=new MetaCloudTemplateTransport(new HttpClient(handler));var result=await t.SendAsync(Account,Template(),Token);Assert.Equal(expected,result.Outcome);Assert.Equal(1,handler.Calls);Assert.DoesNotContain("private",JsonSerializer.Serialize(result));}
}
