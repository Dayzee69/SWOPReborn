using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using HttpMultipartParser;

namespace SWOPReborn
{
    public partial class SWOPReborn : ServiceBase
    {
        public SWOPReborn()
        {
            InitializeComponent();

        }

        protected override void OnStart(string[] args)
        {
            Logger.InitLogger();

            try
            {
                Logger.Log.Info("Начало работы");
                Listen();
            }
            catch (Exception ex)
            {
                Logger.Log.Error(ex);
            }

        }

        private static async Task Listen()
        {
            string strPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            INIManager manager = new INIManager(strPath + @"\settings.ini");

            string hostStr = "http://" + manager.GetPrivateString("SWOPSettings", "host") + "/";

            string pathStr = manager.GetPrivateString("SWOPSettings", "path");
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(hostStr);
            listener.Start();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding win1251 = Encoding.GetEncoding("Windows-1251");

            Logger.Log.Info("Ожидание подключений...");
            while (true)
            {
                
                HttpListenerContext context = await listener.GetContextAsync();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;
                string partnerName = "";
                string funcName = "";

                try
                {
                    string strUrl = request.RawUrl;
                    strUrl = strUrl.Remove(0, 1);
                    string[] strUrlArr = strUrl.Split('/');
                    partnerName = strUrlArr[0];
                    funcName = strUrlArr[1];

                }
                catch (Exception ex)
                {

                    StreamWriter writer = new StreamWriter(response.OutputStream);
                    Logger.Log.Error("URL ERROR ", ex);
                    response.StatusCode = 400;
                    writer.Write("URL ERROR " + ex);
                    writer.Close();
                }

                if (funcName == "upload" && request.HttpMethod == "POST")
                { 
                    
                    Logger.Log.Info("Подключение upload " + partnerName + request.RemoteEndPoint);

                    try
                    {

                        if (Directory.Exists(pathStr))
                        {

                            MultipartFormDataParser httpParser = MultipartFormDataParser.Parse(request.InputStream);

                            List<FilePart> filesPart = httpParser.Files;

                            string strname = "";
                            string strDate = DateTime.UtcNow.ToString();
                            strDate = strDate.Replace(".", "");
                            strDate = strDate.Replace(" ", "");
                            strDate = strDate.Replace(":", "");
                            byte[] contentByteArr;

                            foreach (FilePart file in filesPart) 
                            {
                                if (file.Name == "file") 
                                {
                                    strname = file.FileName;
                                    Logger.Log.Info(strname);
                                    using (BinaryReader br = new BinaryReader(file.Data))
                                    {
                                        contentByteArr = br.ReadBytes((int)file.Data.Length);
                                        File.WriteAllBytes(pathStr + @"\ClientUp\" + partnerName + @"\" + strname, contentByteArr);
                                        File.WriteAllBytes(pathStr + @"\ClientUp\" + partnerName + @"\Temp\" + strDate + "_" + strname, contentByteArr);
                                    }
                                }
                            }

                            if (!Directory.Exists(pathStr + @"\ClientUp\" + partnerName))
                            {
                                Directory.CreateDirectory(pathStr + @"\ClientUp\" + partnerName);
                                Directory.CreateDirectory(pathStr + @"\ClientUp\" + partnerName + @"\Temp\");
                            }

                            byte[] array = File.ReadAllBytes(Path.Combine(pathStr, "ClientUp", partnerName, strname));
                            MD5 md5 = new MD5CryptoServiceProvider();
                            byte[] retVal = md5.ComputeHash(array);

                            StreamWriter writer = new StreamWriter(response.OutputStream, win1251);

                            writer.Write(BitConverter.ToString(retVal).Replace("-", "").ToUpperInvariant());

                            Logger.Log.Info("Файл " + strname + " получен");
                            writer.Close();

                        }
                        else
                        {
                            throw new Exception("NOT FOUND");
                        }
                    }
                    catch (Exception ex)
                    {
                        StreamWriter writer = new StreamWriter(response.OutputStream);

                        Logger.Log.Error("FILE UPLOAD ERROR ", ex);
                        response.StatusCode = 404;
                        writer.Write("FILE UPLOAD ERROR " + ex);
                        writer.Close();
                    }

                    Logger.Log.Info("Подключение upload окончено");
                    Logger.Log.Info("Ожидание подключений...");
                }
                else if (funcName == "download" && request.HttpMethod == "GET")
                {

                    Logger.Log.Info("Подключение download " + partnerName + request.RemoteEndPoint);
                    
                    try
                    {

                        if (Directory.Exists(pathStr + @"\ClientDown\" + partnerName + @"\"))
                        {
                            DirectoryInfo directoryInfo = new DirectoryInfo(pathStr + @"\ClientDown\" + partnerName + @"\");
                            List<FileInfo> dirs = directoryInfo.GetFiles("*", SearchOption.TopDirectoryOnly).OrderBy(t => t.CreationTime).ToList();

                            if (dirs.Count() != 0)
                            {
                                byte[] buffer = File.ReadAllBytes(pathStr + @"\ClientDown\" + partnerName + @"\" + dirs.First().ToString());
                                response.ContentLength64 = buffer.Length;
                                Stream output = response.OutputStream;
                                output.Write(buffer, 0, buffer.Length);
                                output.Close();

                                if (!dirs.First().ToString().Contains("check_"))
                                {
                                    File.Move(pathStr + @"\ClientDown\" + partnerName + @"\" + dirs.First().ToString(),
                                    pathStr + @"\ClientDown\" + partnerName + @"\check_" + dirs.First().ToString());
                                }

                                Logger.Log.Info("Файл " + dirs.First().ToString() + " отправлен");
                            }
                            else 
                            {
                                throw new Exception("NO FILES TO SEND");
                            }
                        }
                        else
                        {
                            throw new Exception("CLIENT NOT FOUND");
                        }

                    }
                    catch (Exception ex)
                    {
                        StreamWriter writer = new StreamWriter(response.OutputStream);
                        response.StatusCode = 404;
                        Logger.Log.Error("FILE DOWNLOAD ERROR ", ex);
                        writer.Write("FILE DOWNLOAD ERROR " + ex);
                        writer.Close();
                    }

                    Logger.Log.Info("Подключение download окончено");
                    Logger.Log.Info("Ожидание подключений...");
                }
                else if (funcName == "check" && request.HttpMethod == "GET")
                {
                    string strDate = DateTime.UtcNow.ToString();
                    strDate = strDate.Replace(".", "");
                    strDate = strDate.Replace(" ", "");
                    strDate = strDate.Replace(":", "");

                    Logger.Log.Info("Подключение check " + partnerName + request.RemoteEndPoint);

                    try
                    {
                        if (Directory.Exists(pathStr + @"\ClientDown\" + partnerName))
                        {
                            DirectoryInfo directoryInfo = new DirectoryInfo(pathStr + @"\ClientDown\" + partnerName + @"\");
                            List<FileInfo> dirs = directoryInfo.GetFiles("check_*", SearchOption.TopDirectoryOnly).OrderBy(t => t.CreationTime).ToList();

                            if (dirs.Count() != 0)
                            {
                                File.Move(pathStr + @"\ClientDown\" + partnerName + @"\" + dirs.First().ToString(),
                                pathStr + @"\ClientDown\" + partnerName + @"\Temp\" + strDate + "_" + dirs.First().ToString());


                                StreamWriter writer = new StreamWriter(response.OutputStream);
                                writer.WriteLine(response.StatusCode);
                                writer.Close();
                                Logger.Log.Info("Файл " + dirs.First().ToString() + " удалён");
                            }
                            else 
                            {
                                throw new Exception("NO FILES TO DELETE");
                            }
                        }
                        else
                        {
                            throw new Exception("CLIENT NOT FOUND");
                        }

                    }
                    catch (Exception ex)
                    {
                        StreamWriter writer = new StreamWriter(response.OutputStream);
                        response.StatusCode = 404;
                        Logger.Log.Error("FILE DELETE ERROR ", ex);
                        writer.Write("FILE DELETE ERROR " + ex);
                        writer.Close();
                    }

                    Logger.Log.Info("Подключение check окончено");
                    Logger.Log.Info("Ожидание подключений...");
                }
                else 
                {
                    StreamWriter writer = new StreamWriter(response.OutputStream);
                    Logger.Log.Error("URL ERROR");
                    response.StatusCode = 400;
                    writer.Write("URL ERROR");
                    writer.Close();
                }
            }
        }

        protected override void OnStop()
        {
            Logger.Log.Info("Прекращение работы");
        }
    }
}
