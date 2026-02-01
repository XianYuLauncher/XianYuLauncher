using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XianYuLauncher.Core.Helpers
{
    public class ServerStatusFetcher
    {
        public static async Task<(string? IconBase64, string Motd, int Online, int Max, long Delay)> PingerAsync(string host, int port = 25565)
        {
            try
            {
                // 0. SRV Resolution (Simple Implementation via nslookup)
                // Only try SRV if port is default 25565 (Minecraft's behavior usually implies SRV only automatically on default port or specific handling, 
                // but strictly speaking launcher should look up SRV if no port specified in UI. Since we pass 25565 as default, we check.)
                if (port == 25565 && !System.Net.IPAddress.TryParse(host, out _))
                {
                    try 
                    {
                        var srv = await ResolveDnsSrvAsync("_minecraft._tcp." + host);
                        if (srv != null)
                        {
                            host = srv.Value.Target;
                            port = srv.Value.Port;
                            System.Diagnostics.Debug.WriteLine($"[Pinger] SRV Resolved: {host}:{port}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Pinger] SRV Lookup Failed: {ex.Message}");
                    }
                }

                // DNS Resolution
                var addresses = await System.Net.Dns.GetHostAddressesAsync(host);
                if (addresses.Length == 0) return (null, "无法解析域名", 0, 0, -1);
                var ip = addresses.First(a => a.AddressFamily == AddressFamily.InterNetwork); // Prefer IPv4
                ip ??= addresses.First();

                // 简单计时
                var sw = System.Diagnostics.Stopwatch.StartNew();
                
                using var client = new TcpClient();
                var cancelTask = Task.Delay(5000);
                var connectTask = client.ConnectAsync(ip, port);
                
                var completedTask = await Task.WhenAny(connectTask, cancelTask);
                if (completedTask == cancelTask)
                {
                    return (null, "连接超时", 0, 0, -1);
                }
                
                // Allow connectTask to propagate exceptions
                await connectTask;

                if (!client.Connected)
                    return (null, "无法连接", 0, 0, -1);

                using var stream = client.GetStream();
                // Don't close stream when writer closes
                using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
                using var reader = new BinaryReader(stream, Encoding.UTF8, true);

                // 1. Handshake
                var handshake = new List<byte>();
                handshake.Add(0x00); // Packet ID
                WriteVarInt(handshake, 47); // Protocol Version (47 = 1.8)
                WriteString(handshake, host); // Host (Use original hostname for VHost)
                handshake.AddRange(BitConverter.GetBytes((ushort)port).Reverse()); // Port
                WriteVarInt(handshake, 1); // Next State: 1 (Status)

                WritePacket(writer, handshake.ToArray());
                writer.Flush();

                // 2. Request
                WritePacket(writer, new byte[] { 0x00 });
                writer.Flush();

                sw.Stop();
                long latency = sw.ElapsedMilliseconds;

                // 3. Response
                int length = ReadVarInt(reader);
                int packetId = ReadVarInt(reader);

                if (packetId != 0x00)
                {
                   return (null, $"协议错误 (ID:{packetId})", 0, 0, latency);
                }

                string json = ReadString(reader);
                System.Diagnostics.Debug.WriteLine($"[Pinger] Response: {json}");
                
                // Parse JSON
                try 
                {
                    var jsonNode = JsonNode.Parse(json);
                    
                    int online = 0;
                    int max = 0;
                    if (jsonNode["players"] is JsonNode playersNode)
                    {
                        online = playersNode["online"]?.GetValue<int>() ?? 0;
                        max = playersNode["max"]?.GetValue<int>() ?? 0;
                    }
                    
                    string motd = "";
                    if (jsonNode["description"] is JsonNode descNode)
                    {
                        if (descNode is JsonObject descObj)
                        {
                            motd = descObj["text"]?.GetValue<string>() ?? "";
                            if (descObj["extra"] is JsonArray extra)
                            {
                                foreach(var item in extra)
                                {
                                    if (item == null) continue;

                                    if (item is JsonObject obj)
                                    {
                                        motd += obj["text"]?.GetValue<string>() ?? "";
                                    }
                                    else if (item is JsonValue val)
                                    {
                                        motd += val.GetValue<string>() ?? "";
                                    }
                                }
                            }
                        }
                        else
                        {
                            motd = descNode.GetValue<string>() ?? "";
                        }
                    }
                    // Remove color codes roughly
                    motd = System.Text.RegularExpressions.Regex.Replace(motd, "§[0-9a-fk-or]", "");
                    
                    string? icon = jsonNode["favicon"]?.GetValue<string>();

                    return (icon, motd, online, max, latency);
                }
                catch
                {
                    return (null, "数据解析失败", 0, 0, latency);
                }
            }
            catch (Exception ex)
            {
                return (null, $"Error: {ex.Message}", 0, 0, -1);
            }
        }

        private static async Task<(string Target, int Port)?> ResolveDnsSrvAsync(string query)
        {
            try
            {
                using var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = "nslookup";
                process.StartInfo.Arguments = $"-type=srv {query}";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                string? target = null;
                int? port = null;

                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var l = line.Trim();
                    // Output format: svr hostname = mc.hypixel.net
                    // Note: Depending on locale, this might vary. 
                    // But 'svr hostname' and 'port' are fairly standard in nslookup output.
                    if (l.Contains("svr hostname", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = l.Split('=');
                        if (parts.Length > 1) target = parts[1].Trim();
                    }
                    else if (l.Contains("port", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = l.Split('=');
                        if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out int p)) port = p;
                    }
                }
                
                // If target or port is missing, maybe try regex backup for localization?
                // For now, assume English output or consistent identifiers.

                if (!string.IsNullOrEmpty(target) && port.HasValue)
                {
                    // Handle trailing dot in DNS (mc.hypixel.net.)
                    if (target.EndsWith(".")) target = target.Substring(0, target.Length - 1);
                    return (target, port.Value);
                }
            }
            catch {}
            return null;
        }

        #region Protocol Helpers
        private static void WritePacket(BinaryWriter writer, byte[] data)
        {
            var buffer = new List<byte>();
            WriteVarInt(buffer, data.Length);
            buffer.AddRange(data);
            writer.Write(buffer.ToArray());
        }

        private static void WriteVarInt(List<byte> buffer, int value)
        {
            while ((value & 128) != 0)
            {
                buffer.Add((byte)((value & 127) | 128));
                value = (int)((uint)value >> 7);
            }
            buffer.Add((byte)value);
        }

        private static int ReadVarInt(BinaryReader reader)
        {
            int value = 0;
            int size = 0;
            int b;
            while (((b = reader.ReadByte()) & 0x80) == 0x80)
            {
                value |= (b & 0x7F) << (size++ * 7);
                if (size > 5) throw new IOException("VarInt too long");
            }
            return value | ((b & 0x7F) << (size * 7));
        }

        private static void WriteString(List<byte> buffer, string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            WriteVarInt(buffer, bytes.Length);
            buffer.AddRange(bytes);
        }

        private static string ReadString(BinaryReader reader)
        {
            int length = ReadVarInt(reader);
            byte[] bytes = reader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }
        #endregion
    }
}
