using System.Text.RegularExpressions;

namespace HCMS4.Tests.Integration
{
    /// <summary>Helpers for posting MVC forms that carry an antiforgery token.</summary>
    public static class FormHelper
    {
        private static readonly Regex TokenPattern = new(
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"",
            RegexOptions.Compiled);

        public static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
        {
            var html = await client.GetStringAsync(url);
            var match = TokenPattern.Match(html);
            if (!match.Success)
            {
                throw new InvalidOperationException($"No antiforgery token found on {url}.");
            }
            return match.Groups[1].Value;
        }

        public static async Task<HttpResponseMessage> PostFormAsync(
            HttpClient client, string formUrl, string postUrl, Dictionary<string, string> fields)
        {
            var token = await GetAntiforgeryTokenAsync(client, formUrl);
            var content = new FormUrlEncodedContent(
                fields.Append(new KeyValuePair<string, string>("__RequestVerificationToken", token)));
            return await client.PostAsync(postUrl, content);
        }
    }
}
