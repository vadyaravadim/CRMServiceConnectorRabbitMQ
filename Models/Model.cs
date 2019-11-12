using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using CrmServiceBus.RequestController;
using Newtonsoft.Json;

namespace CrmServiceBus.Models
{
    public struct EntityConsts
    {
        public const string Employee = "Employee";
        public const string JobPosition = "JobPosition";
        public const string Department = "Department";
        public const string StaffTable = "StaffTable";
        public const string SendEmployeeHistory = "SendEmployeeHistory";
        public const string SendCalendar = "SendCalendar";
        public const string SendVacation = "SendVacation";
    }

    public struct BalancerThread
    {
        public static readonly object basicLocker = new object[] {};
    }
    public struct HttpRequest
    {   
        public static WebResponse webResponse;
    }
    public struct ConstantApp
    {
        public static readonly string LOG_INFO = "Info";
        public static readonly string LOG_WARN = "Warn";
        public static readonly string LOG_ERROR = "Error";
    }

    #region Integration app settings setup
    public class ModelAppSettings
    {
        public static ModelAppSettings LinkToObject { get; set; }
        public string AppUrl { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public string RabbitMQAuthUrl { get; set; }
        public string UserSessionTimeout { get; set; }
        public Dictionary<string, string> CollectionService { get; set; }
        public List<ApplicationSettings> ApplicationSettings { get; set; }
        public static ModelAppSettings StartSetupSettingApp() => Controller.SetupSettingBus();
    }

    public class ApplicationSettings
    {
        public string Exchange { get; set; }
        public string QueuName { get; set; }
        public string Type { get; set; }
    }
    #endregion

    #region Interface request
    /// <summary>
    /// Интерфейс, описывающий методы для работы запросов
    /// </summary>
    public interface IModelExecuteMethod
    {
        Dictionary<string, string> MappingCollectionMethods();
        object RequestCRM(HttpWebRequest httpWebRequest, object appClassRequest);
    }
    #endregion

    #region Realisation inteface for integration
    /// <summary>
    /// Реализация интерфейса для запросов в биллинг через crm
    /// </summary>
    public class ImplementationRequestBilling : IModelExecuteMethod
    {
        public object RequestCRM(HttpWebRequest httpWebRequest, object appClassRequest = null)
        {
            return Controller.ExecuteMethodBilling<object>(httpWebRequest);
        }

        public Dictionary<string, string> MappingCollectionMethods() => ModelAppSettings.LinkToObject.CollectionService;
    }

    /// <summary>
    /// Реализация интерфейса для запросов из 1С через crm
    /// </summary>
    public class ImplementationRequest1C : IModelExecuteMethod
    {
        public object RequestCRM(HttpWebRequest httpWebRequest, object appClassRequest)
        {
            return Controller.ExecuteMethod1C<object>(httpWebRequest, appClassRequest);
        }

        public Dictionary<string, string> MappingCollectionMethods() => ModelAppSettings.LinkToObject.CollectionService;
    }
    #endregion

    #region General block for setting request crm application
    public static class RequestHelper
    {
        public static CookieContainer AuthCookie = new CookieContainer();
        public static string AuthServiceUri { get => $"{ModelAppSettings.LinkToObject.AppUrl}/ServiceModel/AuthService.svc/Login"; }
    }

    /// <summary>
    /// Класс содержащий типовые настройки для запроса в систему
    /// </summary>
    public class GeneralSettingRequest
    {
        public const string POST = "POST";
        public const string ContentTypeJSON = "application/json";
        public static CookieContainer CookieContainer
        {
            get => RequestHelper.AuthCookie;
        }
        public HttpWebRequest SettingMethodRequest(string method, string requestUrl)
        {
            lock (Controller.httpLocker)
            {
                HttpWebRequest authRequest = WebRequest.Create(requestUrl) as HttpWebRequest;
                authRequest.Method = method;
                authRequest.ContentType = ContentTypeJSON;
                authRequest.CookieContainer = CookieContainer;
                if (authRequest.CookieContainer.Count >= 4)
                {
                    Controller.AddAuthKeyInHeader(ref authRequest);
                }
                return authRequest;
            }
        }
    }

    /// <summary>
    /// Наследованный клас от основного, для реализации авторизационного запроса
    /// </summary>
    public class RequestAuthServiceClass : GeneralSettingRequest
    {
        public string UserName { get => ModelAppSettings.LinkToObject.Login; }
        public string UserPassword { get => ModelAppSettings.LinkToObject.Password; }
        private static int UserSessionTimeout { get => Convert.ToInt32(ModelAppSettings.LinkToObject.UserSessionTimeout); }
        private static DateTime LastTimeAuthAppControl { get; set; }
        internal static DateTime LastTimeAuthApp
        {
            get => LastTimeAuthAppControl;
            set => LastTimeAuthAppControl = value.AddMinutes(UserSessionTimeout);
        }

        /// <summary>
        /// Метод, выполняющий авторизацию в CRM
        /// Авторизуется один раз, при обработке последующих сообщений из шины
        /// Смотрит на последнюю дату авторизации + Время жизни cookie
        /// Если время жизни истекает, делает ещё один запрос в CRM на авторизацию
        /// </summary>
        public void AuthRequest()
        {
            if (CookieContainer.Count == 0 || LastTimeAuthApp <= DateTime.Now)
            {
                ResponseAuthService responseAuth = AuthBeforeRequest(GeneralSettingRequest.POST);
                if (responseAuth?.Code != 0)
                    throw new Exception(JsonConvert.SerializeObject(responseAuth));
            }
        }

        private ResponseAuthService AuthBeforeRequest(string method)
        {
            HttpWebRequest webRequest = SettingMethodRequest(method, RequestHelper.AuthServiceUri);
            return Controller.AuthServiceRequest(webRequest, this);
        }
    }

    /// <summary>
    /// Основной класс описывающий все запросы проходящие через шину
    /// Включает в себя реализацию основного класса с настройками
    /// </summary>
    public class RequestApplicationClass : GeneralSettingRequest
    {
        private const string Integration1C = "1C";
        private const string IntegrationBilling = "Billing";

        private static string entityName;
        public static string IntegrationService { get; set; }
        /// <summary>
        /// Результирующее поле, после маппинга переданного ключа, содержит Url запрос API CRM
        /// </summary>
        private static object EntityName { get => entityName; set => entityName = Controller.CheckEntityValue(value); }
        private object DataRequest { get; set; }
        public RequestApplicationClass(object objectRequest)
        {
            EntityName = objectRequest ?? throw new ArgumentNullException(nameof(objectRequest));
            DataRequest = objectRequest;
        }

        /// <summary>
        /// Метод выполняющий запросы по API в CRM
        /// </summary>
        /// <param name="method">Принимает метод запроса</param> 
        /// <returns></returns>
        public object Request(string method)
        {
            Dictionary<string, string> MappingCollectionMethods = new ImplementationRequestBilling().MappingCollectionMethods();
            string getServiceUri = ReturnCurrentUrlApi(MappingCollectionMethods);
            HttpWebRequest webRequest = SettingMethodRequest(method, getServiceUri);
            switch (IntegrationService)
            {
                case (Integration1C):
                    return new ImplementationRequest1C().RequestCRM(webRequest, DataRequest);
                case (IntegrationBilling):
                    return new ImplementationRequestBilling().RequestCRM(webRequest);
            }
            return null;
        }

        public static string ReturnCurrentUrlApi(Dictionary<string, string> MappingCollectionMethods, string nameRequest = "")
        {
            string curretUrlRequest = MappingCollectionMethods.Where(index => index.Key.ToLower() == (string.IsNullOrEmpty(nameRequest) ? EntityName.ToString().ToLower() : nameRequest.ToString().ToLower())).Select(index => index.Value).FirstOrDefault();
            if (string.IsNullOrEmpty(curretUrlRequest)) return string.Empty;
            if (curretUrlRequest.ToLower().Contains(Integration1C.ToLower())) IntegrationService = Integration1C;
            if (curretUrlRequest.ToLower().Contains(IntegrationBilling.ToLower())) IntegrationService = IntegrationBilling;
            return ModelAppSettings.LinkToObject.AppUrl + curretUrlRequest;
        }
    }

    public class ExecuteDataBase
    {
        [JsonProperty("entityName")]
        public string EntityName { get; set; }
    }

    public class ResponseAuthService
    {
        public int Code { get; set; }
        public string Message { get; set; }
        public ResponseAuthServiceException Exception { get; set; }
        public string PasswordChangeUrl { get; set; }
        public string RedirectUrl { get; set; }
    }

    public class ResponseAuthServiceException
    {
        public string HelpLink { get; set; }
        public string InnerException { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public string Type { get; set; }
    }
    #endregion

    #region Description class for request integration method billing in crm

    public class ExecuteDataBilling : ExecuteDataBase
    {
        [JsonProperty("timeStamp")]
        public Int64 TimeStamp { get; set; }
        [JsonProperty("entityId")]
        public int EntityId { get; set; }
    }
    #endregion

    #region Description class for request integration method 1c in crm
    public class DataIntegration1C<TRecords>
    {
        public string EntityName { get; set; }
        public RequestData1C<TRecords> Data { get; set; }
    }

    public class RequestData1C<TRecords>
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string PersonnelNumber { get; set; }
        public string FullName { get; set; }
        public string StaffTableId { get; set; }
        public string Email { get; set; }
        public string ReceiptDate { get; set; }
        public string DismissalDate { get; set; }
        public string СompletionTrialPeriodDate { get; set; }
        public string DepartmentId { get; set; }
        public string JobPositionId { get; set; }
        public string EmploymentType { get; set; }
        public string ContractType { get; set; }
        public string Code { get; set; }
        public string IsSeparate { get; set; }
        public string KPP { get; set; }
        public string IsFormed { get; set; }
        public string CreatedDate { get; set; }
        public string IsDisbanded { get; set; }
        public string DisbandmentDate { get; set; }
        public string ParentId { get; set; }
        public string IsApproved { get; set; }
        public string ApprovedDate { get; set; }
        public string IsClosed { get; set; }
        public string ClosedDate { get; set; }
        public string StaffCategory { get; set; }
        public string PeriodDate { get; set; }
        public string Registrar { get; set; }
        public string EventType { get; set; }
        public int Rate { get; set; }
        public string EmploymentId { get; set; }
        public string DayType { get; set; }
        public string Date { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string AllegedEndDate { get; set; }
        public string State { get; set; }
        public List<TRecords> Records { get; set; }
    }

    public class SendVacation
    {
        public string EmploymentId { get; set; }
        public string Id { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string AllegedEndDate { get; set; }
        public string State { get; set; }
    }

    public class SendEmployeeHistory
    {
        public string Id { get; set; }
        public string PeriodDate { get; set; }
        public string Registrar { get; set; }
        public string EventType { get; set; }
        public string Rate { get; set; }
        public string EmploymentId { get; set; }
        public string DepartmentId { get; set; }
        public string JobPositionId { get; set; }
    }
    #endregion

    #region Class for Logging
    public class ExtensionLogging
    {
        private static string PathAppLog {
            get
            {
                string pathToLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log");
                if (!Directory.Exists(pathToLog))
                    Directory.CreateDirectory(pathToLog);
                return pathToLog;
            }
        }
        private static string PrivateFileName { get; set; }
        public static string FileName { get => PrivateFileName; set => PrivateFileName = Path.Combine(PathAppLog, string.Format("{0}_{1}_{2:dd.MM.yyy}.log", value,
                AppDomain.CurrentDomain.FriendlyName, DateTime.Now)); }
        private protected static object sync = new object();

    }

    public class Log : ExtensionLogging
    {
        public static object Sync { get => sync; set => sync = value; }
        public static void Write(Exception ex, string requestJson = "")
        {
            try
            {
                ExtensionLogging.FileName = ConstantApp.LOG_ERROR;
                string fullText = string.Format("{0}[{1:dd.MM.yyy HH:mm:ss.fff}] [{2}.{3}()] [{4}.{5}]\r\n", Environment.NewLine,
                DateTime.Now, ex.TargetSite.DeclaringType, ex.TargetSite.Name, ex.Message, requestJson);
                lock (Sync)
                {
                    File.AppendAllText(ExtensionLogging.FileName, fullText, Encoding.UTF8);
                }
            }
            catch {}
        }

        public static void Write(string LevelLogging, string msg)
        {
            try
            {
                ExtensionLogging.FileName = LevelLogging;
                string fullText = string.Format("{0}[{1:dd.MM.yyy HH:mm:ss.fff}] [{2}]\r\n", Environment.NewLine,
                DateTime.Now, msg);
                lock (Sync)
                {
                    File.AppendAllText(ExtensionLogging.FileName, fullText, Encoding.UTF8);
                }
            }
            catch {}
        }
    }
    #endregion
}
