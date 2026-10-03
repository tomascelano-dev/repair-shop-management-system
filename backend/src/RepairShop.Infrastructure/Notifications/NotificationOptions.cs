namespace RepairShop.Infrastructure.Notifications;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Background dispatcher on/off (disabled in tests that drive it manually).</summary>
    public bool DispatcherEnabled { get; set; } = true;
    public int PollSeconds { get; set; } = 10;
    public int BatchSize { get; set; } = 20;

    /// <summary>Per channel provider: None | Simulated | Twilio | Meta (WhatsApp) | Smtp (Email).</summary>
    public ChannelOptions WhatsApp { get; set; } = new();
    public ChannelOptions Sms { get; set; } = new();
    public ChannelOptions Email { get; set; } = new();

    public TwilioOptions Twilio { get; set; } = new();
    public MetaOptions Meta { get; set; } = new();
    public SmtpOptions Smtp { get; set; } = new();

    public sealed class ChannelOptions
    {
        public string Provider { get; set; } = "None";
    }

    public sealed class TwilioOptions
    {
        public string AccountSid { get; set; } = "";
        public string AuthToken { get; set; } = "";
        /// <summary>WhatsApp sender, e.g. "whatsapp:+14155238886".</summary>
        public string WhatsAppFrom { get; set; } = "";
        /// <summary>SMS sender number, e.g. "+15005550006".</summary>
        public string SmsFrom { get; set; } = "";
        public string BaseUrl { get; set; } = "https://api.twilio.com/";
    }

    public sealed class MetaOptions
    {
        public string PhoneNumberId { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string ApiVersion { get; set; } = "v21.0";
        /// <summary>
        /// Optional approved template with a single body variable ({{1}}) used outside the 24h customer service window.
        /// Empty = send plain text (only works within the window).
        /// </summary>
        public string TemplateName { get; set; } = "";
        public string TemplateLanguage { get; set; } = "es_AR";
        public string BaseUrl { get; set; } = "https://graph.facebook.com/";
    }

    public sealed class SmtpOptions
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string FromAddress { get; set; } = "";
        public string FromName { get; set; } = "RepairShop";
        /// <summary>Auto | StartTls | SslOnConnect | None</summary>
        public string Security { get; set; } = "Auto";
    }
}
