using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using CrmServiceBus.Models;
using Newtonsoft.Json;

namespace CrmServiceBus.RequestController
{
    class Controller
    {
        /// <summary>
        /// Установка значений для сервиса из конфигурационного файла
        /// </summary>
        /// <returns></returns>
        public static ModelAppSettings SetupSettingBus()
        {
            string[] foundFileSetting = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "SettingsIntegration.json", SearchOption.AllDirectories);
            string readText = File.ReadAllText(foundFileSetting.First());
            return JsonConvert.DeserializeObject<ModelAppSettings>(readText);
        }

        /// <summary>
        /// Поиск в переданном объекте параметра entityName, возвращает ключ, по которому будет API запрос
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="requestObject"></param>
        /// <returns>возвращает ключ, по которому будет API запрос</returns>
        public static string CheckEntityValue<T>(T requestObject) => (string)requestObject.GetType().GetProperties()
                .Where(index => index.ToString().Split()[1].ToLower() == "entityname")
                .Select(index => index.GetValue(requestObject, null))
                .FirstOrDefault();

        /// <summary>
        /// Вспомогательный метод, вызывающий API методы (биллинг) в CRM
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="httpWebRequest"></param>
        /// <returns></returns>
        public static T ExecuteMethodBilling<T>(HttpWebRequest httpWebRequest) => GetResponseStreamService<T>(httpWebRequest);

        /// <summary>
        /// Вспомогательный метод, вызывающий API методы (1C) в CRM
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="httpWebRequest"></param>
        /// <param name="applicationClassRequest"></param>
        /// <returns></returns>
        public static object ExecuteMethod1C<T>(HttpWebRequest httpWebRequest, T applicationClassRequest)
        {
            object requestData = MappingCollectionMethod1C(applicationClassRequest);

            GetRequestStreamService<T>(httpWebRequest, requestData);
            return GetResponseStreamService<T>(httpWebRequest);
        }

        /// <summary>
        /// Маппинг коллекции методов для корректного составления json 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="applicationClassRequest"></param>
        /// <returns></returns>
        private static object MappingCollectionMethod1C<T>(T applicationClassRequest)
        {
            DataIntegration1C data1C = applicationClassRequest as DataIntegration1C;
            Log.Write(JsonConvert.SerializeObject(applicationClassRequest));
            string stringJsonObject = JsonConvert.SerializeObject(data1C.Data);
            object requestData;

            switch (data1C.EntityName)
            {
                case "Employee":
                    requestData = new Dictionary<string, RequestData1C>() {
                        { "employeeData", JsonConvert.DeserializeObject<RequestData1C>(stringJsonObject) }
                    };
                    break;
                case "JobPosition":
                    requestData = new Dictionary<string, RequestData1C>() {
                        { "jobPositionData", JsonConvert.DeserializeObject<RequestData1C>(stringJsonObject) }
                    };
                    break;
                case "Department":
                    requestData = new Dictionary<string, RequestData1C>() {
                        { "departmentData", JsonConvert.DeserializeObject<RequestData1C>(stringJsonObject) }
                    };
                    break;
                case "StaffTable":
                    requestData = new Dictionary<string, RequestData1C>() {
                        { "staffTableData", JsonConvert.DeserializeObject<RequestData1C>(stringJsonObject) }
                    };
                    break;
                case "SendEmployeeHistory":
                    requestData = new Dictionary<string, RequestData1C>() {
                        { "sendEmployeeHistoryData", JsonConvert.DeserializeObject<RequestData1C>(stringJsonObject) }
                    };
                    break;
                case "SendCalendar":
                    requestData = new Dictionary<string, RequestData1C>() {
                        { "sendCalendarData", JsonConvert.DeserializeObject<RequestData1C>(stringJsonObject) }
                    };
                    break;
                case "SendVacation":
                    requestData = new Dictionary<string, RequestData1C>() {
                        { "sendVacationData", JsonConvert.DeserializeObject<RequestData1C>(stringJsonObject) }
                    };
                    break;
                default:
                    throw new Exception("Not supported request method");
            }

            return requestData;
        }

        #region Request for Authorization in crm
        /// <summary>
        /// Основной авторизационный метод
        /// </summary>
        /// <param name="httpWebRequest">Принимает и инициализирует экземпляр HttpWebRequest для последующего запроса</param>
        /// <param name="applicationClassLogin">Принимает и инициализирует экземпляр RequestAuthServiceClass для последующего запроса</param>
        public static ResponseAuthService AuthServiceRequest(HttpWebRequest httpWebRequest, RequestAuthServiceClass applicationClassLogin)
        {
            return SendRequestAuthService(httpWebRequest, applicationClassLogin);
        }

        private static ResponseAuthService SendRequestAuthService(HttpWebRequest httpWebRequest, RequestAuthServiceClass applicationClassLogin)
        {
            GetRequestStreamService<RequestAuthServiceClass>(httpWebRequest, applicationClassLogin);
            return GetResponseStreamService<ResponseAuthService>(httpWebRequest);
        }

        private static void GetRequestStreamService<T>(HttpWebRequest httpWebRequest, object applicationClassRequest)
        {
            using (Stream requestStream = httpWebRequest.GetRequestStream())
            {
                using (StreamWriter writer = new StreamWriter(requestStream))
                {
                    writer.Write(JsonConvert.SerializeObject(applicationClassRequest));
                }
            }
        }

        private static T GetResponseStreamService<T>(HttpWebRequest httpWebRequest)
        {
            using (HttpWebResponse response = (HttpWebResponse)httpWebRequest.GetResponse())
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    SettingLastTimeAuth(response);
                    T reponse = JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
                    Log.Write(JsonConvert.SerializeObject(reponse));
                    return reponse;
                }
            }
        }

        private static void SettingLastTimeAuth(HttpWebResponse response)
        {
            if (response.ResponseUri.AbsolutePath.Contains("AuthService.svc/Login"))
                RequestAuthServiceClass.LastTimeAuthApp = response.LastModified;
        }

        internal static void AddAuthKeyInHeader(ref HttpWebRequest httpWebRequest)
        {
            foreach (Cookie cookie in httpWebRequest.CookieContainer.GetCookies(new Uri(ModelAppSettings.LinkToObject.AppUrl)))
            {
                if (cookie.Name.Contains("BPMCSRF"))
                    httpWebRequest.Headers.Add(cookie.Name, cookie.Value);
            }
        }

        #endregion
    }
}
