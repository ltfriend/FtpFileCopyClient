using System;
using System.IO;
using System.Threading.Tasks;

using CoreFtp;

namespace FTPFileCopyClient {
    class Program {
        /// <summary>
        /// Usage: ftpcp [options] source [user@]host:target
        /// 
        /// Options:
        ///     -p port   connect to specified port
        ///     -l user   connect with specified username
        ///     -pw passw login with specified password
        ///     -a        anonymous connection
        ///     -pasv     use passive mode
        ///     
        /// </summary>
        static int Main(string[] args) {
            ShowAppInfo();

            if (args.Length == 0) {
                ShowUsageDescription();
                return 0;
            } else if (!ParseParams(args)) {
                ShowHelpMessage();
                return -1;
            }

            if (!CheckSourceFile())
                return 3;

            int result = RequestCredentials();
            if (result != 0)
                return result;

            FtpClientConfiguration configuration = new FtpClientConfiguration {
                Host = host,
                BaseDirectory = path
            };

            if (port != 0) {
                configuration.Port = port;
            }
            if (anonymous) {
                configuration.Username = "anonymous";
                configuration.Password = "mail@mail.com";
            } else {
                configuration.Username = username;
                configuration.Password = password;
            }
            if (pasvMode) {
                //TODO: set passive mode
            }

            Task task = TransferFileAsync(configuration);
            task.Wait();

            return resultCode;

        }

        private static string source = null;
        private static string host;
        private static int port = 0;
        private static string path;
        private static bool anonymous = false;
        private static string username = null;
        private static string password = null;
        private static bool pasvMode = false;

        private static int resultCode = 0;

        private const string appVersion = "1.0.1";

        private static int RequestCredentials() {
            if (string.IsNullOrEmpty(username)) {
                Console.Write($"{host} username: ");
                username = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(username)) {
                    Console.WriteLine("username is require");
                    return 2;
                }
            }

            if (string.IsNullOrEmpty(password)) {
                Console.Write($"{host} password: ");
                password = "";

                bool leftEnter = false;
                while (!leftEnter) {
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == '\r')
                        leftEnter = true;
                    else
                        password += key.KeyChar;
                }

                Console.WriteLine();

                if (string.IsNullOrWhiteSpace(password)) {
                    Console.WriteLine("password is require");
                    return 2;
                }
            }

            return 0;
        }

        private static bool CheckSourceFile() {
            if (!File.Exists(source)) {
                Console.WriteLine($"{source}: no such file");
                return false;
            } else {
                var fileAttrs = File.GetAttributes(source);
                if ((fileAttrs & FileAttributes.Directory) == FileAttributes.Directory) {
                    Console.WriteLine($"{source}: is directory");
                    return false;
                }
            }

            return true;
        }

        private static Task TransferFileAsync(FtpClientConfiguration configuration) {
            return Task.Run(async () => {
                using var client = new FtpClient(configuration);

                try {
                    await client.LoginAsync();
                } catch (Exception e) {
                    Console.WriteLine(e.Message);
                    resultCode = 1;
                    return;
                }

                FileInfo fileInfo = new FileInfo(source);

                Stream writeStream;
                try {
                    writeStream = await client.OpenFileWriteStreamAsync(fileInfo.Name);
                } catch (Exception e) {
                    Console.WriteLine(e.Message);
                    resultCode = 1;
                    return;
                }

                Stream readStream = null;
                try {
                    readStream = fileInfo.OpenRead();
                    await readStream.CopyToAsync(writeStream);
                } catch (Exception e) {
                    Console.WriteLine(e.Message);
                    resultCode = 1;
                    return;
                } finally {
                    readStream?.Dispose();
                    writeStream.Dispose();
                }

                Console.WriteLine($"{source}: upload success");
            });
        }

        #region Command line arguments

        private static string GetOptionArgument(string[] args, int argNum) {
            if (argNum < args.Length) {
                var arg = args[argNum];
                return arg[0] == '-' ? null : arg;
            } else
                return null;
        }

        private static bool ParseParams(string[] args) {
            string target = null;

            int i = 0;
            while (i < args.Length) {
                var arg = args[i];

                if (arg[0] == '-') {
                    switch (arg) {
                        case "-p":
                            string portStr = GetOptionArgument(args, i + 1);

                            if (string.IsNullOrEmpty(portStr)) {
                                ShowErrorMessageRequiresArgument(arg);
                                return false;
                            }

                            if (!int.TryParse(portStr, out port)) {
                                ShowErrorMessage($"invalid argument option \"{arg}\", value must be number");
                                return false;
                            } else if (port < 1 || port > 65536) {
                                ShowErrorMessage($"invalid argument option \"{arg}\", value must be number in range 1..65535");
                                return false;
                            }

                            i++;
                            break;
                        case "-l":
                            username = GetOptionArgument(args, i + 1);

                            if (string.IsNullOrEmpty(username)) {
                                ShowErrorMessageRequiresArgument(arg);
                                return false;
                            }

                            i++;
                            break;
                        case "-pw":
                            password = GetOptionArgument(args, i + 1);

                            if (string.IsNullOrEmpty(password)) {
                                ShowErrorMessageRequiresArgument(arg);
                                return false;
                            }

                            i++;
                            break;
                        case "-a":
                            anonymous = true;
                            break;
                        case "-pasv":
                            pasvMode = true;
                            break;
                        default:
                            ShowErrorMessage($"unknown option \"{arg}\"");
                            return false;
                    }
                } else if (source == null) {
                    source = arg;
                } else if (target == null) {
                    target = arg;
                } else {
                    ShowErrorMessage("too many arguments");
                    return false;
                }

                i++;
            }

            if (source == null) {
                ShowErrorMessage("no source file specified");
                return false;
            } else if (target == null) {
                ShowErrorMessage("no target FTP specified");
                return false;
            }

            // Parse target
            int pos = target.IndexOf('@');
            if (pos >= 0) {
                username = target.Substring(0, pos);
                target = target.Substring(pos + 1);
            }

            pos = target.IndexOf(':');
            if (pos < 0) {
                host = target;
                path = "/";
            } else {
                host = target.Substring(0, pos);
                path = target.Substring(pos + 1);
                if (string.IsNullOrEmpty(path))
                    path = "/";
            }

            if (string.IsNullOrEmpty(host)) {
                ShowErrorMessage("no target FTP specified");
                return false;
            }

            return true;
        }

        private static void ShowAppInfo() {
            Console.WriteLine("FTP files copy client");
            Console.WriteLine($"Version: {appVersion}");
        }

        private static void ShowUsageDescription() {
            Console.WriteLine("Usage: ftpcp [options] source [user@]host:target");
            Console.WriteLine("");
            Console.WriteLine("Options:");
            Console.WriteLine("    -p port   connect to specified port");
            Console.WriteLine("    -l user   connect with specified username");
            Console.WriteLine("    -pw passw login with specified password");
            Console.WriteLine("    -a        anonymous connection");
            Console.WriteLine("    -pasv     use passive mode");
        }

        private static void ShowHelpMessage() {
            Console.WriteLine("       try typing just \"ftpcp\" for help");
        }

        private static void ShowErrorMessage(string message) {
            Console.WriteLine($"ftpcp: {message}");
        }

        private static void ShowErrorMessageRequiresArgument(string arg) {
            ShowErrorMessage($"option \"{arg}\" requires an argument");
        }

        #endregion
    }
}