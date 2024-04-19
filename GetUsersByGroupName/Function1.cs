using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using GetUsersByGroupName.Models;

namespace GetUsersByGroupName
{
    public class GetUsersByGroupName
    {
        private static readonly string graphApiUrl = "https://graph.microsoft.com/v1.0";
        private static readonly string tenantId = "df29b2fa-8929-482f-9dbb-60ff4df224c4";
        private static readonly string clientId = "1c623cd3-98ca-4c7d-b622-93d13cb831c3";
        private static readonly string graphDefaultUrl = "https://graph.microsoft.com/.default";
        private static readonly string loginUrl = "https://login.microsoftonline.com";
        private IConfiguration _configuration;

        public GetUsersByGroupName(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [Function("GetUsersByGroupName")]
        public  async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequestData req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("GetUsersByGroupName");

            // Parse query parameter
            string groupName = req.Query["groups"]!;

            if (string.IsNullOrEmpty(groupName))
            {
                var response = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                response.WriteString("Please provide a group name.");
                return response;
            }

            try
            {
                string[] groups = groupName.Split(',');
                var accessToken = await GetAccessToken();
                string users = string.Empty;
                List<Value> values = [];
                foreach ( string group in groups )
                {
                    users = await GetUsersInGroup(group, accessToken);
                    Value payLoadObject = JsonSerializer.Deserialize<Value>(users)!;
                    values.Add(payLoadObject);
                }

               // Value payLoadObject = JsonSerializer.Deserialize<Value>(users)!;

                ResponseObject responseObj = FormatOutput(values, groupName, new Payload());
                responseObj.message = "Success";
                string ResponseJsonString = JsonSerializer.Serialize(responseObj);

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                response.WriteString(ResponseJsonString);
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error: {ex.Message}");
                List<Value> values = [];
                ResponseObject responseObj = FormatOutput(values, groupName, new Payload());
                responseObj.status = "500";
                responseObj.message = ex.Message;
                var response = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                string ResponseJsonString = JsonSerializer.Serialize(responseObj);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                response.WriteString(ResponseJsonString);
                return response;
            }
        }

        private ResponseObject FormatOutput(List<Value> values, string groupName, Payload payload)
        {
            ResponseObject response = new();
            response.status = "200";
            payload.upn = [];
            payload.upn.Add(groupName);
            payload.users = [];
            foreach (Value value in values)
            {
                payload = FormatUserPrincipalNames(value, payload);
            }
            
            response.payload = payload;
            return response;
        }

        private Payload FormatUserPrincipalNames(Value value, Payload payload)
        {
            foreach(User user in value.value!)
            {
                payload.users!.Add(user.userPrincipalName);
            }

            return payload;
        }

        private async Task<string> GetAccessToken()
        {
            var client = new HttpClient();
            /*string tenantId = _configuration.GetValue<string>("tenantId");
            string graphDefaultUrl = _configuration.GetValue<string>("graphDefaultUrl");
            string loginUrl = _configuration.GetValue<string>("loginUrl");
            string clientId = _configuration.GetValue<string>("clientId");
            string clientSecret = _configuration.GetValue<string>("clientSecret");*/

            var tokenEndpoint = $"{loginUrl}/{tenantId}/oauth2/v2.0/token";

            var payload = $"client_id={clientId}&scope={graphDefaultUrl}&client_secret={clientSecret}&grant_type=client_credentials";

            var response = await client.PostAsync(tokenEndpoint, new StringContent(payload, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"));
            var tokenResponse = await response.Content.ReadAsStringAsync();
            var tokenObject = JsonSerializer.Deserialize<JsonElement>(tokenResponse);

            return tokenObject.GetProperty("access_token").GetString()!;
        }

        private async Task<string> GetUsersInGroup(string groupName, string accessToken)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            // string graphApiUrl = _configuration.GetValue<string>("graphApiUrl");

            var groupQuery = $"{graphApiUrl}/groups?$filter=displayName eq '{groupName}'&$select=id";
            var groupResponse = await client.GetAsync(groupQuery);
            var groupContent = await groupResponse.Content.ReadAsStringAsync();
            var groupObject = JsonSerializer.Deserialize<JsonElement>(groupContent);

            var groupId = groupObject.GetProperty("value")[0].GetProperty("id").GetString();

            var usersQuery = $"{graphApiUrl}/groups/{groupId}/members?$select=id,displayName,userPrincipalName";
            var usersResponse = await client.GetAsync(usersQuery);
            var usersContent = await usersResponse.Content.ReadAsStringAsync();

            return usersContent;
        }
    }
}
