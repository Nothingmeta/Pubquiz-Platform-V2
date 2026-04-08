namespace Pubquiz_Platform_V2.Middleware
{
    public class JwtCookieMiddleware
    {
        private readonly RequestDelegate _next;

        public JwtCookieMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // If there's a JWT in cookie but no Authorization header, add it
            if (!context.Request.Headers.ContainsKey("Authorization") && 
                context.Request.Cookies.TryGetValue("auth_token", out var token))
            {
                context.Request.Headers.Add("Authorization", $"Bearer {token}");
            }

            await _next(context);
        }
    }
}