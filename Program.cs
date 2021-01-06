using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WP7Checkin
{
    class Program
    {
        static Conf _conf;
        static HttpClient _scClient;
        static readonly int[] _successRets = { 1, 2 };

        static async Task Main()
        {
            _conf = Deserialize<Conf>(GetEnvValue("CONF"));
            if (!string.IsNullOrWhiteSpace(_conf.ScKey))
            {
                _scClient = new HttpClient();
            }
            Uri baseUri = new Uri(_conf.BaseUrl);

            Console.WriteLine("WP7签到开始运行...");
            for (int i = 0; i < _conf.Users.Length; i++)
            {
                User user = _conf.Users[i];
                string title = $"账号 {i + 1}: {user.Task} ";
                Console.WriteLine($"共 {_conf.Users.Length} 个账号，正在运行{title}...");

                SocketsHttpHandler handler = new SocketsHttpHandler { AllowAutoRedirect = false };
                using var client = new HttpClient(handler) { BaseAddress = baseUri };
                client.DefaultRequestHeaders.Referrer = baseUri;
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.198 Safari/537.36");

                //登录账号
                var rspMsg = await client.PostAsync("/login", new StringContent($"log={user.Username}&pwd={user.Password}&rememberme=forever&redirect_to={_conf.BaseUrl}&action=login", Encoding.UTF8, "application/x-www-form-urlencoded"));
                if (rspMsg.StatusCode != HttpStatusCode.Redirect)
                {//登录失败
                    await Notify($"{title}登录失败，请检查账号密码是否正确！", true);
                    continue;
                }

                //签到
                client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                rspMsg = await client.PostAsync("/wp-admin/admin-ajax.php?action=wp_uc_ajax", new StringContent("admininit=0&type=sign", Encoding.UTF8, "application/x-www-form-urlencoded"));
                string result = await rspMsg.Content.ReadAsStringAsync();

                using JsonDocument doc = JsonDocument.Parse(result);
                JsonElement root = doc.RootElement;

                bool isFailed = !_successRets.Contains(root.GetProperty("ret").GetInt32());
                await Notify($"WP7{title}签到{(isFailed ? "失败" : "成功")}！{result}", isFailed);
            }
            Console.WriteLine("签到运行完毕");
        }

        static async Task Notify(string msg, bool isFailed = false)
        {
            Console.WriteLine(msg);
            if (_conf.ScType == "Always" || (isFailed && _conf.ScType == "Failed"))
            {
                await _scClient?.GetAsync($"https://sc.ftqq.com/{_conf.ScKey}.send?text={msg}");
            }
        }

        static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _options);

        static string GetEnvValue(string key) => Environment.GetEnvironmentVariable(key);
    }

    #region Conf

    public class Conf
    {
        public string BaseUrl { get; set; }
        public User[] Users { get; set; }
        public string ScKey { get; set; }
        public string ScType { get; set; }
    }

    public class User
    {
        public string Task { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    #endregion
}
