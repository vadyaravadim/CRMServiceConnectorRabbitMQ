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
        protected internal static readonly object httpLocker = new object();

        /// <summary>
        /// Маппинг коллекции методов для корректного составления json 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="applicationClassRequest"></param>
        /// <returns></returns>
        private static object MappingCollectionMethod1C<T>(HttpWebRequest httpWebRequest, T applicationClassRequest)
        {
            DataIntegration1C<object> data1C = applicationClassRequest as DataIntegration1C<object>;
            string stringJsonObject = JsonConvert.SerializeObject(data1C.Data);
            Dictionary<string, object> requestData = new Dictionary<string, object>();

            switch (data1C.EntityName)
            {
                case EntityConsts.Employee:
                    httpWebRequest = new GeneralSettingRequest().SettingMethodRequest(GeneralSettingRequest.POST, RequestApplicationClass.ReturnCurrentUrlApi(new ImplementationRequestBilling().MappingCollectionMethods(), EntityConsts.Employee));

                    requestData = new Dictionary<string, object>() {
                        { "employeeData", JsonConvert.DeserializeObject<RequestData1C<object>>(stringJsonObject) }
                    };
                    break;
                case EntityConsts.JobPosition:
                    httpWebRequest = new GeneralSettingRequest().SettingMethodRequest(GeneralSettingRequest.POST, RequestApplicationClass.ReturnCurrentUrlApi(new ImplementationRequestBilling().MappingCollectionMethods(), EntityConsts.JobPosition));

                    requestData = new Dictionary<string, object>() {
                        { "jobPositionData", JsonConvert.DeserializeObject<RequestData1C<object>>(stringJsonObject) }
                    };
                    break;
                case EntityConsts.Department:
                    httpWebRequest = new GeneralSettingRequest().SettingMethodRequest(GeneralSettingRequest.POST, RequestApplicationClass.ReturnCurrentUrlApi(new ImplementationRequestBilling().MappingCollectionMethods(), EntityConsts.Department));

                    requestData = new Dictionary<string, object>() {
                        { "departmentData", JsonConvert.DeserializeObject<RequestData1C<object>>(stringJsonObject) }
                    };
                    break;
                case EntityConsts.StaffTable:
                    httpWebRequest = new GeneralSettingRequest().SettingMethodRequest(GeneralSettingRequest.POST, RequestApplicationClass.ReturnCurrentUrlApi(new ImplementationRequestBilling().MappingCollectionMethods(), EntityConsts.StaffTable));

                    requestData = new Dictionary<string, object>() {
                        { "staffTableData", JsonConvert.DeserializeObject<RequestData1C<object>>(stringJsonObject) }
                    };
                    break;
                case EntityConsts.SendEmployeeHistory:
                    httpWebRequest = new GeneralSettingRequest().SettingMethodRequest(GeneralSettingRequest.POST, RequestApplicationClass.ReturnCurrentUrlApi(new ImplementationRequestBilling().MappingCollectionMethods(), EntityConsts.SendEmployeeHistory));

                    RequestData1C<SendEmployeeHistory> requestDataEmployeeHistory = JsonConvert.DeserializeObject<RequestData1C<SendEmployeeHistory>>(stringJsonObject);

                    if (requestDataEmployeeHistory?.Records.Count > 0)
                    {
                        requestDataEmployeeHistory?.Records.ForEach(item => item.EmploymentId = requestDataEmployeeHistory.EmploymentId);
                        requestDataEmployeeHistory?.Records.ForEach(item => RequestCollectionData(httpWebRequest,
                            new Dictionary<string, SendEmployeeHistory>() {
                                { "sendEmployeeHistoryData", item }
                            }));
                        return null;
                    }
                    break;
                case EntityConsts.SendCalendar:
                    httpWebRequest = new GeneralSettingRequest().SettingMethodRequest(GeneralSettingRequest.POST, RequestApplicationClass.ReturnCurrentUrlApi(new ImplementationRequestBilling().MappingCollectionMethods(), EntityConsts.SendCalendar));

                    requestData = new Dictionary<string, object>() {
                        { "sendCalendarData", JsonConvert.DeserializeObject<RequestData1C<object>>(stringJsonObject) }
                    };
                    break;
                case EntityConsts.SendVacation:
                    httpWebRequest = new GeneralSettingRequest().SettingMethodRequest(GeneralSettingRequest.POST, RequestApplicationClass.ReturnCurrentUrlApi(new ImplementationRequestBilling().MappingCollectionMethods(), EntityConsts.SendVacation));

                    RequestData1C<SendVacation> requestDataVacation = JsonConvert.DeserializeObject<RequestData1C<SendVacation>>(stringJsonObject);

                    if (requestDataVacation?.Records.Count > 0)
                    {
                        requestDataVacation?.Records.ForEach(item => item.EmploymentId = requestDataVacation.EmploymentId);
                        requestDataVacation?.Records.ForEach(item => RequestCollectionData(httpWebRequest,
                            new Dictionary<string, SendVacation>() {
                                { "sendVacationData", item }
                            }));
                        return null;
                    }
                    break;
                default:
                    throw new Exception("Not supported request method");
            }

            return requestData;
        }

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
        public static T ExecuteMethodBilling<T>(HttpWebRequest httpWebRequest)
        {
            lock (httpLocker)
            {
                try
                {
                    T resultRequest = GetResponseStreamService<T>(httpWebRequest);
                    Log.Write(ConstantApp.LOG_INFO, JsonConvert.SerializeObject(httpWebRequest));
                    return resultRequest;
                }
                catch (Exception ex)
                {
                    Log.Write(ex, JsonConvert.SerializeObject(httpWebRequest));
                    return default;
                }
            }
        }

        /// <summary>
        /// Вспомогательный метод, вызывающий API методы (1C) в CRM
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="httpWebRequest"></param>
        /// <param name="applicationClassRequest"></param>
        /// <returns></returns>
        public static object ExecuteMethod1C<T>(HttpWebRequest httpWebRequest, T applicationClassRequest)
        {
            object requestData = MappingCollectionMethod1C(httpWebRequest, applicationClassRequest);
            if (requestData != null)
            {
                lock (httpLocker)
                {
                    try
                    {
                        GetRequestStreamService<T>(httpWebRequest, requestData);
                        object resultRequest = GetResponseStreamService<T>(httpWebRequest);
                        Log.Write(ConstantApp.LOG_INFO, JsonConvert.SerializeObject(requestData));
                        return resultRequest;
                    }
                    catch (Exception ex)
                    {
                        Log.Write(ex, JsonConvert.SerializeObject(requestData));
                        return null;
                    }
                }
            }
            return null;
        }

        public static object RequestCollectionData(HttpWebRequest httpWebRequest, object requestData)
        {
            lock (httpLocker)
            {
                try
                {
                    httpWebRequest = new GeneralSettingRequest().SettingMethodRequest(GeneralSettingRequest.POST, httpWebRequest.RequestUri.ToString());
                    GetRequestStreamService<object>(httpWebRequest, requestData);
                    object resultRequest = GetResponseStreamService<object>(httpWebRequest);
                    Log.Write(ConstantApp.LOG_INFO, JsonConvert.SerializeObject(requestData));
                    return resultRequest;
                }
                catch (Exception ex)
                {
                    Log.Write(ex, JsonConvert.SerializeObject(requestData));
                    return null;
                }
            }
        }

        #region Request for Authorization in crm
        /// <summary>
        /// Основной авторизационный метод
        /// </summary>
        /// <param name="httpWebRequest">Принимает и инициализирует экземпляр HttpWebRequest для последующего запроса</param>
        /// <param name="applicationClassLogin">Принимает и инициализирует экземпляр RequestAuthServiceClass для последующего запроса</param>
        public static ResponseAuthService AuthServiceRequest(HttpWebRequest httpWebRequest, RequestAuthServiceClass applicationClassLogin)
        {
            lock (httpLocker)
            {
                return SendRequestAuthService(httpWebRequest, applicationClassLogin);
            }
        }

        private static ResponseAuthService SendRequestAuthService(HttpWebRequest httpWebRequest, RequestAuthServiceClass applicationClassLogin)
        {
            lock (httpLocker)
            {
                try
                {
                    GetRequestStreamService<RequestAuthServiceClass>(httpWebRequest, applicationClassLogin);
                    ResponseAuthService responseAuth = GetResponseStreamService<ResponseAuthService>(httpWebRequest);
                    Log.Write(ConstantApp.LOG_INFO, JsonConvert.SerializeObject(applicationClassLogin));
                    return responseAuth;
                }
                catch (Exception ex)
                {
                    Log.Write(ex, JsonConvert.SerializeObject(applicationClassLogin));
                    return default;
                }
            }
        }

        private static void GetRequestStreamService<T>(HttpWebRequest httpWebRequest, object applicationClassRequest)
        {
            lock (BalancerThread.basicLocker)
            {
                using (Stream requestStream = httpWebRequest.GetRequestStream())
                {
                    using (StreamWriter writer = new StreamWriter(requestStream))
                    {
                        writer.Write(JsonConvert.SerializeObject(applicationClassRequest));
                        requestStream.Close();
                    }
                    requestStream.Dispose();
                }
            }
        }

        private static T GetResponseStreamService<T>(HttpWebRequest httpWebRequest)
        {
            lock (BalancerThread.basicLocker)
            {
                using (HttpWebResponse response = (HttpWebResponse)(httpWebRequest.GetResponse()))
                {
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        SettingLastTimeAuth(response);
                        T responseRequest = JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
                        response.Dispose();

                        return responseRequest;
                    }
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
