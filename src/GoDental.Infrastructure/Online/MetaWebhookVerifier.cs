using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
namespace GoDental.Infrastructure.Online;

// No efecto de negocio antes de verificar firma y cuenta. El host central debe deduplicar
// ExternalId y persistir atómicamente antes de responder 200; este parser no es un receptor publicado.
public static class MetaWebhookVerifier
{
    // appSecret usa UTF-8 del valor original de Meta, sin decodificar sus caracteres hexadecimales.
    public const int MaxBodyBytes=256*1024;
    public static Result<MetaWebhookBatch> Verify(ReadOnlyMemory<byte> body,string? signature,ReadOnlySpan<byte> appSecret,MetaCloudAccount account,DateTimeOffset now)
    {
        if(account is null||body.Length is 0 or >MaxBodyBytes||appSecret.Length<32||signature is null||signature.Length!=71||!signature.StartsWith("sha256=",StringComparison.Ordinal)||!Digits(account.PhoneNumberId,32)||!Digits(account.BusinessAccountId,32))return Invalid("SIGNATURE");
        byte[] provided;try{provided=Convert.FromHexString(signature[7..]);}catch(FormatException){return Invalid("SIGNATURE");}
        var expected=HMACSHA256.HashData(appSecret,body.Span);bool valid;
        try{valid=CryptographicOperations.FixedTimeEquals(expected,provided);}finally{CryptographicOperations.ZeroMemory(expected);CryptographicOperations.ZeroMemory(provided);}
        if(!valid)return Invalid("SIGNATURE");
        try
        {
            using var doc=JsonDocument.Parse(body,new JsonDocumentOptions{MaxDepth=32});var root=doc.RootElement;
            if(HasDuplicateProperties(root)||Value(root,"object",60)!="whatsapp_business_account"||!Array(root,"entry",out var entries)||entries.GetArrayLength()>20)return Invalid("PAYLOAD");
            var messages=new List<MetaInboundMessage>();var statuses=new List<MetaMessageStatus>();var ids=new HashSet<string>(StringComparer.Ordinal);var byId=new Dictionary<string,MetaInboundMessage>(StringComparer.Ordinal);
            foreach(var entry in entries.EnumerateArray())
            {
                if(Value(entry,"id",32)!=account.BusinessAccountId)return Invalid("ACCOUNT");
                if(!Array(entry,"changes",out var changes)||changes.GetArrayLength()>50)return Invalid("PAYLOAD");
                foreach(var change in changes.EnumerateArray())
                {
                    if(Value(change,"field",60)!="messages")continue;
                    if(!change.TryGetProperty("value",out var value)||Value(value,"messaging_product",40)!="whatsapp"||!value.TryGetProperty("metadata",out var metadata)||Value(metadata,"phone_number_id",32)!=account.PhoneNumberId)return Invalid("ACCOUNT");
                    if(value.TryGetProperty("messages",out var incoming))
                    {
                        if(incoming.ValueKind!=JsonValueKind.Array||incoming.GetArrayLength()>100)return Invalid("PAYLOAD");
                        foreach(var item in incoming.EnumerateArray())
                        {
                            var id=Value(item,"id",200);var from=Value(item,"from",15);var type=Value(item,"type",40);var date=Timestamp(item,now);
                            if(id is null||!Digits(from,15)||from!.Length<7||type is null||!type.All(c=>char.IsAsciiLetterLower(c)||c=='_')||date is null)return Invalid("PAYLOAD");
                            string? text=null;
                            if(type=="text")
                            {
                                if(!item.TryGetProperty("text",out var content)||(text=Value(content,"body",4096,true))is null)return Invalid("PAYLOAD");
                            }
                            var message=new MetaInboundMessage(id,"+"+from,type,text,date.Value);if(byId.TryGetValue(id,out var previous)&&previous!=message)return Invalid("CONFLICT");if(byId.TryAdd(id,message))messages.Add(message);
                        }
                    }
                    if(value.TryGetProperty("statuses",out var updates))
                    {
                        if(updates.ValueKind!=JsonValueKind.Array||updates.GetArrayLength()>100)return Invalid("PAYLOAD");
                        foreach(var update in updates.EnumerateArray())
                        {
                            var id=Value(update,"id",200);var recipient=Value(update,"recipient_id",15);var state=Value(update,"status",20);var date=Timestamp(update,now);
                            if(id is null||!Digits(recipient,15)||recipient!.Length<7||state is not("sent" or "delivered" or "read" or "failed")||date is null)return Invalid("PAYLOAD");
                            int? error=null;
                            if(update.TryGetProperty("errors",out var errors)&&errors.ValueKind==JsonValueKind.Array&&errors.GetArrayLength()>0&&errors[0].TryGetProperty("code",out var code)&&code.TryGetInt32(out var n))error=n;
                            if(ids.Add($"status:{id}:{state}:{date.Value.ToUnixTimeSeconds()}"))statuses.Add(new(id,"+"+recipient,state,date.Value,error));
                        }
                    }
                    if(messages.Count+statuses.Count>200)return Invalid("PAYLOAD");
                }
            }
            return Result<MetaWebhookBatch>.Success(new(messages.AsReadOnly(),statuses.AsReadOnly()));
        }
        catch(Exception ex)when(ex is JsonException or InvalidOperationException or FormatException or ArgumentOutOfRangeException){return Invalid("PAYLOAD");}
    }
    public static bool VerifySubscription(string? mode,string? suppliedToken,string configuredToken,string? challenge)
    {
        if(mode!="subscribe"||configuredToken.Length<32||suppliedToken is null||suppliedToken.Length!=configuredToken.Length||challenge is null||challenge.Length is 0 or >512||!challenge.All(char.IsAsciiDigit))return false;
        var expected=System.Text.Encoding.UTF8.GetBytes(configuredToken);var supplied=System.Text.Encoding.UTF8.GetBytes(suppliedToken);
        try{return CryptographicOperations.FixedTimeEquals(expected,supplied);}finally{CryptographicOperations.ZeroMemory(expected);CryptographicOperations.ZeroMemory(supplied);}
    }
    private static bool HasDuplicateProperties(JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Object){var names=new HashSet<string>(StringComparer.Ordinal);foreach(var p in value.EnumerateObject())if(!names.Add(p.Name)||HasDuplicateProperties(p.Value))return true;}
        else if(value.ValueKind==JsonValueKind.Array)foreach(var e in value.EnumerateArray())if(HasDuplicateProperties(e))return true;
        return false;
    }
    private static bool Array(JsonElement root,string key,out JsonElement array){array=default;return root.ValueKind==JsonValueKind.Object&&root.TryGetProperty(key,out array)&&array.ValueKind==JsonValueKind.Array;}
    private static bool Digits(string? s,int max)=>s is {Length:>0}&&s.Length<=max&&s.All(char.IsAsciiDigit);
    private static string? Value(JsonElement e,string name,int max,bool multiline=false)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.String&&v.GetString() is {} s&&s.Length>0&&s.Length<=max&&!s.Any(c=>char.IsControl(c)&&(!multiline||c is not('\r' or '\n' or '\t')))?s:null;
    private static DateTimeOffset? Timestamp(JsonElement e,DateTimeOffset now)
    {
        var raw=Value(e,"timestamp",20);if(!long.TryParse(raw,NumberStyles.None,CultureInfo.InvariantCulture,out var seconds))return null;
        var date=DateTimeOffset.FromUnixTimeSeconds(seconds);return date.Year>=2000&&date<=now.AddMinutes(5)?date:null;
    }
    private static Result<MetaWebhookBatch> Invalid(string code)=>Result<MetaWebhookBatch>.Failure("META_"+code,"Notificación de Meta no verificada. No se aplicaron efectos de negocio.");
}
