using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GoDental.Application.DTOs;
namespace GoDental.Infrastructure.Online;

// Adaptador de servidor; no se registra en WPF. Su invocador debe exigir identidad confiable,
// autorización comercial, exclusiones vigentes, aprobación, plantilla oficial y outbox durable.
// No se utiliza este adaptador para hacer envíos de prueba a contactos reales.
public sealed class MetaCloudTemplateTransport:IDisposable
{
    private readonly HttpClient client;
    public MetaCloudTemplateTransport():this(new HttpClient(new SocketsHttpHandler{AllowAutoRedirect=false,UseCookies=false,Credentials=null,MaxResponseHeadersLength=16,ConnectTimeout=TimeSpan.FromSeconds(10)}){Timeout=Timeout.InfiniteTimeSpan}){}
    public MetaCloudTemplateTransport(HttpClient client)=>this.client=client;
    public async Task<MetaDeliveryResult> SendAsync(MetaCloudAccount account,MetaTemplateMessage message,string accessToken,CancellationToken ct=default)
    {
        if(account is null||message is null||!Regex.IsMatch(account.ApiVersion??"",@"\Av[1-9][0-9]{0,2}\.[0-9]{1,2}\z")||!Digits(account.PhoneNumberId,32)||!Digits(account.BusinessAccountId,32)||message.RequestId==Guid.Empty||!Digits(message.Recipient,15)||message.Recipient.Length<7||!Regex.IsMatch(message.Template??"",@"\A[a-z0-9_]{1,512}\z")||!Regex.IsMatch(message.Language??"",@"\A[a-z]{2,3}(?:_[A-Z]{2})?\z")||message.BodyParameters is null||message.BodyParameters.Count>10||message.BodyParameters.Any(s=>s is null||s.Length is 0 or >1024||s.Any(c=>char.IsControl(c)&&c is not('\r' or '\n' or '\t')))||string.IsNullOrWhiteSpace(accessToken)||accessToken.Length is <32 or >4096||accessToken.Any(char.IsWhiteSpace)||accessToken.Any(char.IsControl))throw new ArgumentException("Revisa la configuración del servidor y la plantilla oficial. No se realizó un envío.");
        ct.ThrowIfCancellationRequested();
        var parameters=message.BodyParameters.Select(text=>new{type="text",text}).ToArray();
        var template=new{name=message.Template,language=new{code=message.Language},components=parameters.Length==0?Array.Empty<object>():new object[]{new{type="body",parameters}}};
        var payload=JsonSerializer.Serialize(new{messaging_product="whatsapp",recipient_type="individual",to=message.Recipient,type="template",template});
        using var request=new HttpRequestMessage(HttpMethod.Post,$"https://graph.facebook.com/{account.ApiVersion}/{account.PhoneNumberId}/messages"){Content=new StringContent(payload,Encoding.UTF8,"application/json")};
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",accessToken);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
            if(response.Content.Headers.ContentLength is >65536)return Unknown(message);
            await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);using var memory=new MemoryStream();var bytes=new byte[4096];int n;
            while((n=await stream.ReadAsync(bytes,timeout.Token))>0){if(memory.Length+n>65536)return Unknown(message);await memory.WriteAsync(bytes.AsMemory(0,n),timeout.Token);}
            using var doc=JsonDocument.Parse(memory.ToArray(),new JsonDocumentOptions{MaxDepth=20});var root=doc.RootElement;
            if(root.ValueKind!=JsonValueKind.Object)return Unknown(message);
            if(response.IsSuccessStatusCode&&Text(root,"messaging_product")=="whatsapp"&&root.TryGetProperty("messages",out var messages)&&messages.ValueKind==JsonValueKind.Array&&messages.GetArrayLength()==1&&Text(messages[0],"id") is {} external&&external.StartsWith("wamid.",StringComparison.Ordinal)&&external.Length<=200&&!external.Any(char.IsControl))
            {
                if(!root.TryGetProperty("contacts",out var contacts)||contacts.ValueKind!=JsonValueKind.Array||contacts.GetArrayLength()!=1||Text(contacts[0],"wa_id")!=message.Recipient)return Unknown(message);
                return new(message.RequestId,MetaDeliveryOutcome.Accepted,external);
            }
            if((int)response.StatusCode is >=400 and <500&&root.TryGetProperty("error",out var error)&&error.ValueKind==JsonValueKind.Object&&error.TryGetProperty("code",out var code)&&code.TryGetInt32(out var providerCode)&&providerCode>0)return new(message.RequestId,MetaDeliveryOutcome.Rejected,ProviderErrorCode:providerCode);
            return Unknown(message);
        }
        catch(Exception ex)when(ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or System.IO.IOException){return Unknown(message);}
    }
    private static string? Text(JsonElement e,string property)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(property,out var p)&&p.ValueKind==JsonValueKind.String?p.GetString():null;
    private static bool Digits(string? s,int limit)=>s is {Length:>0}&&s.Length<=limit&&s.All(char.IsAsciiDigit);
    private static MetaDeliveryResult Unknown(MetaTemplateMessage r)=>new(r.RequestId,MetaDeliveryOutcome.Uncertain);
    public void Dispose()=>client.Dispose();
}
