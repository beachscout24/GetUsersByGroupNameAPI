using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using GetUsersByGroupName.Models;
using GetUsersByGroupName.Utilities;

namespace GetUsersByGroupName
{
    public class GetUsersByGroupName
    {
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
                response.WriteString(Constants.PROVIDEGROUPNAME);
                return response;
            }
            List<Value> values = [];
            try
            {
                // break groups into array fro iteration
                string[] groups = groupName.Split(',');
                var accessToken = await GetAccessToken();
                string users = string.Empty;
                // iterate thru every group and get the members/users for each group
                foreach ( string group in groups )
                {
                    users = await GetUsersInGroup(group, accessToken);
                    Value payLoadObject = JsonSerializer.Deserialize<Value>(users)!;
                    values.Add(payLoadObject);
                }

                // format the output to return upns as well as status,message, and group names
                ResponseObject responseObj = FormatOutput(values, groupName, new Payload());
                responseObj.message = Constants.SUCCESS;
                string ResponseJsonString = JsonSerializer.Serialize(responseObj);

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", Constants.CONTENT_TYPE);
                response.WriteString(ResponseJsonString);
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError($"{Constants.ERROR}{ex.Message}");
                ResponseObject responseObj = FormatOutput(values, groupName, new Payload());
                responseObj.status = "500";
                if(ex.Message == Constants.EXCEPTION)
                {
                    responseObj.message = Constants.EXCEPTION_MESSAGE;
                }
                else
                {
                    responseObj.message = $"{Constants.EXCEPTION_PREAMBLE} {ex.Message}";
                }
                
                var response = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                string ResponseJsonString = JsonSerializer.Serialize(responseObj);
                response.Headers.Add("Content-Type", Constants.CONTENT_TYPE);
                response.WriteString(ResponseJsonString);
                return response;
            }
        }

        private ResponseObject FormatOutput(List<Value> values, string groupName, Payload payload)
        {
            ResponseObject response = new();
            response.status = Constants.TWOHUNDRED;
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
            string tenantId = _configuration.GetValue<string>(Constants.TENANTID);
            string graphDefaultUrl = _configuration.GetValue<string>(Constants.GRAPHDEFAULTURL);
            string loginUrl = _configuration.GetValue<string>(Constants.LOGINURL);
            string clientId = _configuration.GetValue<string>(Constants.CLIENTID);
            string clientAppId = _configuration.GetValue<string>(Constants.CLIENTSECRET);

            var tokenEndpoint = $"{loginUrl}/{tenantId}/oauth2/v2.0/token";

            var payload = $"client_id={clientId}&scope={graphDefaultUrl}&client_secret={clientAppId}&grant_type=client_credentials";

            var response = await client.PostAsync(tokenEndpoint, new StringContent(payload, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"));
            var tokenResponse = await response.Content.ReadAsStringAsync();
            var tokenObject = JsonSerializer.Deserialize<JsonElement>(tokenResponse);

            return tokenObject.GetProperty(Constants.ACCESSTOKEN).GetString()!;
        }

        private async Task<string> GetUsersInGroup(string groupName, string accessToken)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(Constants.BEARER, accessToken);
            string graphApiUrl = _configuration.GetValue<string>(Constants.GRAPHAPIURL);

            var groupQuery = $"{graphApiUrl}/groups?$filter=displayName eq '{groupName}'&$select=id";
            var groupResponse = await client.GetAsync(groupQuery);
            var groupContent = await groupResponse.Content.ReadAsStringAsync();
            var groupObject = JsonSerializer.Deserialize<JsonElement>(groupContent);

            var groupId = groupObject.GetProperty(Constants.VALUE)[0].GetProperty(Constants.ID).GetString();

            var usersQuery = $"{graphApiUrl}/groups/{groupId}/members?$select=id,displayName,userPrincipalName";
            var usersResponse = await client.GetAsync(usersQuery);
            var usersContent = await usersResponse.Content.ReadAsStringAsync();

            return usersContent;
        }
    }
}
