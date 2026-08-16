using Microsoft.AspNetCore.Http;

namespace AkmazBackend.Helpers
{
    public static class RoleHelper
    {
        public static bool IsAdmin(HttpRequest request) =>
            request.Headers["role"].ToString() == "admin";

        public static bool IsAuditor(HttpRequest request) =>
            request.Headers["role"].ToString() == "auditor";
    }
}
