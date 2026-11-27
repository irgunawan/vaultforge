using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace HermesVaultManager
{
    internal sealed class VaultService
    {
        private readonly AppSettings _settings;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public VaultService(AppSettings settings)
        {
            _settings = settings;
        }

        public string BridgeScript
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vault_bridge.ps1"); }
        }

        public VaultResult AddLogin(string label, string origin, string identifierType,
            string identifier, string password, string otpSecret)
        {
            var payload = new Dictionary<string, object>
            {
                { "kind", "login" },
                { "label", label },
                { "origin", origin },
                { "identifier_type", identifierType },
                { "identifier", identifier },
                { "password", password },
                { "otp_secret", otpSecret ?? "" }
            };
            return Execute("add", payload);
        }

        public VaultResult List()
        {
            return Execute("list", null);
        }

        public VaultResult Remove(string handle)
        {
            return Execute("rm", new Dictionary<string, object> { { "handle", handle } });
        }

        public VaultResult KeyInfo()
        {
            return Execute("key", null);
        }

        private VaultResult Execute(string action, object payload)
        {
            var result = new VaultResult();
            string tempFile = null;
            try
            {
                if (!File.Exists(BridgeScript))
                {
                    result.Error = "vault_bridge.ps1 tidak ditemukan di folder aplikasi.";
                    return result;
                }

                string arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(BridgeScript)
                    + " -Action " + Quote(action)
                    + " -Python " + Quote(_settings.PythonExe ?? "python.exe")
                    + " -HermesHome " + Quote(_settings.HermesHome ?? "");

                if (payload != null)
                {
                    tempFile = Path.Combine(Path.GetTempPath(),
                        "hvm-" + Guid.NewGuid().ToString("N") + ".json");
                    File.WriteAllText(tempFile, _json.Serialize(payload), new UTF8Encoding(false));
                    arguments += " -InputFile " + Quote(tempFile);
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = new Process { StartInfo = psi })
                {
                    var stdout = new StringBuilder();
                    var stderr = new StringBuilder();
                    process.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (!process.WaitForExit(90000))
                    {
                        try { process.Kill(); } catch { }
                        result.Error = "PowerShell timeout setelah 90 detik.";
                        return result;
                    }
                    process.WaitForExit();

                    string output = stdout.ToString().Trim();
                    string error = stderr.ToString().Trim();
                    result.Raw = string.IsNullOrWhiteSpace(error) ? output : (output + "\n" + error);

                    if (process.ExitCode != 0)
                    {
                        result.Error = string.IsNullOrWhiteSpace(error) ? output : error;
                        if (string.IsNullOrWhiteSpace(result.Error))
                        {
                            result.Error = "PowerShell exit code " + process.ExitCode;
                        }
                        return result;
                    }

                    if (string.IsNullOrWhiteSpace(output))
                    {
                        result.Ok = true;
                        return result;
                    }

                    int jsonStart = output.IndexOf('{');
                    int jsonEnd = output.LastIndexOf('}');
                    if (jsonStart >= 0 && jsonEnd > jsonStart)
                    {
                        output = output.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    }

                    var parsed = _json.DeserializeObject(output) as Dictionary<string, object>;
                    if (parsed == null)
                    {
                        result.Ok = true;
                        return result;
                    }

                    result.Ok = GetBool(parsed, "ok", true);
                    result.Error = GetString(parsed, "error");
                    result.Handle = GetString(parsed, "handle");

                    object itemsObj;
                    if (parsed.TryGetValue("items", out itemsObj))
                    {
                        var list = itemsObj as IEnumerable;
                        if (list != null)
                        {
                            foreach (var entry in list)
                            {
                                var d = entry as Dictionary<string, object>;
                                if (d == null) continue;
                                result.Items.Add(new VaultItem
                                {
                                    Handle = GetString(d, "handle"),
                                    Kind = GetString(d, "kind"),
                                    Label = GetString(d, "label"),
                                    Identifier = GetString(d, "identifier"),
                                    Origin = GetString(d, "origin")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }
            finally
            {
                if (tempFile != null)
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
            return result;
        }

        private static string GetString(Dictionary<string, object> d, string key)
        {
            object value;
            if (d.TryGetValue(key, out value) && value != null) return Convert.ToString(value);
            return "";
        }

        private static bool GetBool(Dictionary<string, object> d, string key, bool fallback)
        {
            object value;
            if (d.TryGetValue(key, out value) && value != null)
            {
                bool b;
                if (bool.TryParse(Convert.ToString(value), out b)) return b;
            }
            return fallback;
        }

        private static string Quote(string value)
        {
            if (value == null) return "\"\"";
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
