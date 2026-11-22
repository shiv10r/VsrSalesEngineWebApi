namespace VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

/// <summary>Shared mailbox names (info@, hr@ ...) as opposed to a named person's address.</summary>
public static class RoleAccountClassifier
{
    private static readonly HashSet<string> RoleMailboxes = new(StringComparer.OrdinalIgnoreCase)
    {
        "info", "hr", "jobs", "careers", "hello", "contact", "team", "support", "sales", "admin", "recruiting",
        "recruitment", "hiring", "office", "press", "media", "help", "enquiries", "inquiries", "mail", "marketing"
    };

    public static bool IsRoleAccount(string email)
    {
        var at = email.IndexOf('@');
        var local = at > 0 ? email[..at] : email;
        return RoleMailboxes.Contains(local.Trim());
    }
}
