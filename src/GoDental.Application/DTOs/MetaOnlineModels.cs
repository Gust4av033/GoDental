namespace GoDental.Application.DTOs;
public sealed record MetaCloudAccount(string ApiVersion,string PhoneNumberId,string BusinessAccountId);
public sealed record MetaTemplateMessage(Guid RequestId,string Recipient,string Template,string Language,IReadOnlyList<string> BodyParameters);
public enum MetaDeliveryOutcome { Accepted,Rejected,Uncertain }
public sealed record MetaDeliveryResult(Guid RequestId,MetaDeliveryOutcome Outcome,string? ExternalMessageId=null,int? ProviderErrorCode=null);
public sealed record MetaInboundMessage(string ExternalId,string Sender,string Type,string? Text,DateTimeOffset Timestamp);
public sealed record MetaMessageStatus(string ExternalId,string Recipient,string State,DateTimeOffset Timestamp,int? ErrorCode);
public sealed record MetaWebhookBatch(IReadOnlyList<MetaInboundMessage> Messages,IReadOnlyList<MetaMessageStatus> Statuses);
