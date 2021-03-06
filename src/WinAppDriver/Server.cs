using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace WinAppDriver {

    class Server {

        private RequestManager requestManager;

        public Server(RequestManager requestManager) {
            this.requestManager = requestManager;
        }

        public void Start() {
            var listener = new HttpListener();
            listener.Prefixes.Add("http://+:4444/wd/hub/");
            listener.Start();

            Console.WriteLine("Listening...");
            while (true) {
                var context = listener.GetContext();
                var request = context.Request;
                var response = context.Response;
                HandleRequest(request, response);
            }
        }

        private void HandleRequest(HttpListenerRequest request, HttpListenerResponse response) {
            string method = request.HttpMethod;
            string path = request.Url.AbsolutePath;
            string body = new StreamReader(request.InputStream, request.ContentEncoding).ReadToEnd();
            Console.WriteLine("Request: {0} {1}\n{2}", method, path, body);

            Session session = null;
            try {
                object result = requestManager.Handle(method, path, body, out session);
                ResponseResult(method, path, result, session, response);
            } catch (Exception ex) {
                ResponseException(method, path, ex, session, response);
            }
        }

        private void ResponseResult(string method, string path, object result,
            Session session, HttpListenerResponse response)
        {
            var message = new Dictionary<string, object> {
                { "sessionId", (session != null ? session.ID : null) },
                { "status", 0 },
                { "value", result }
            };

            string json = JsonConvert.SerializeObject(message);
            WriteResponse(response, HttpStatusCode.OK, "application/json", json);
        }

        private void ResponseException(string method, string path, Exception ex,
            Session session, HttpListenerResponse response)
        {
            string body = null;
            var httpStatus = HttpStatusCode.InternalServerError;
            string contentType = "application/json";

            if (ex is FailedCommandException)
            {
                var message = new Dictionary<string, object>
                {
                    { "sessionId", (session != null ? session.ID : null) },
                    { "status", ((FailedCommandException)ex).Code },
                    { "value", new Dictionary<string, object>
                        {
                            { "message", ex.Message } // TODO stack trace
                        }
                    }
                };

                body = JsonConvert.SerializeObject(message);
            }
            else
            {
                httpStatus = HttpStatusCode.BadRequest;
                contentType = "text/plain";
                body = ex.ToString();
            }

            WriteResponse(response, httpStatus, contentType, body);
        }

        private void WriteResponse(HttpListenerResponse response, HttpStatusCode httpStatus,
            string contentType, string body)
        {
            string bodyHead = body.Length > 200 ? body.Substring(0, 200) : body;
            Console.WriteLine("Response (Status: {0}, ContentType: {1}):\n{2}",
                              httpStatus, contentType, bodyHead);

            response.StatusCode = (int)httpStatus;
            response.ContentType = contentType;
            response.ContentEncoding = Encoding.UTF8;
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(body);
            response.ContentLength64 = buffer.Length;
            var output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
        }
    }

}

