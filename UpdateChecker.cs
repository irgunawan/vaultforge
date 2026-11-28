using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace VaultForge
{
    internal static class UpdateChecker
    {
        public const string Repo = "irgunawan/vaultforge";
        public const string ApiUrl = "https://api.github.com/repos/" + Repo + "/releases/latest";

        public static UpdateInfo Check(string currentVersion)
        {
            var info = new UpdateInfo();
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var request = (HttpWebRequest)WebRequest.Create(ApiUrl);
                request.UserAgent = "VaultForge/" + currentVersion;
                request.Accept = "application/vnd.github+json";
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    var body = reader.ReadToEnd();
                    var json = new JavaScriptSerializer();
                    var root = json.DeserializeObject(body) as Dictionary<string, object>;
                    if (root == null)
                    {
                        info.Error = "Respons GitHub tidak dikenali.";
                        return info;
                    }

                    string tag = GetString(root, "tag_name");
                    info.Version = (tag ?? "").TrimStart('v', 'V');
                    info.ReleaseUrl = GetString(root, "html_url");
                    info.Notes = GetString(root, "body");

                    object assetsObj;
                    if (root.TryGetValue("assets", out assetsObj))
                    {
                        var assets = assetsObj as IEnumerable;
                        if (assets != null)
                        {
                            foreach (var entry in assets)
                            {
                                var asset = entry as Dictionary<string, object>;
                                if (asset == null) continue;
                                string name = GetString(asset, "name");
                                if (string.IsNullOrEmpty(name)) continue;
                                string url = GetString(asset, "browser_download_url");
                                object sizeObj;
                                long size = 0;
                                if (asset.TryGetValue("size", out sizeObj) && sizeObj != null)
                                {
                                    try { size = Convert.ToInt64(sizeObj); }
                                    catch { size = 0; }
                                }

                                if (name.Equals("VaultForge.exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    info.ExeUrl = url;
                                    info.ExeSize = size;
                                }
                                else if (name.Equals("VaultForge.exe.sha256", StringComparison.OrdinalIgnoreCase))
                                {
                                    info.ExeShaUrl = url;
                                }
                                else if (name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                                {
                                    info.MsiUrl = url;
                                    info.MsiSize = size;
                                }
                                else if (name.EndsWith(".msi.sha256", StringComparison.OrdinalIgnoreCase))
                                {
                                    info.MsiShaUrl = url;
                                }
                            }
                        }
                    }

                    Version current, latest;
                    if (!Version.TryParse(currentVersion, out current))
                    {
                        current = new Version(0, 0, 0);
                    }
                    if (!Version.TryParse(info.Version, out latest))
                    {
                        latest = current;
                    }
                    info.Available = latest > current;
                }
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse &&
                    ((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
                {
                    info.Error = "Belum ada release di GitHub.";
                }
                else
                {
                    info.Error = ex.Message;
                }
            }
            catch (Exception ex)
            {
                info.Error = ex.Message;
            }
            return info;
        }

        private static string GetString(Dictionary<string, object> d, string key)
        {
            object value;
            if (d.TryGetValue(key, out value) && value != null) return Convert.ToString(value);
            return "";
        }
    }
}
