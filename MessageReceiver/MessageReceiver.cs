using System;
using System.Text;
using System.Threading;
using CrmServiceBus.Models;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace CrmServiceBus.Receiver
{
    public class MessageReceiver : DefaultBasicConsumer
    {
        private readonly IModel _channel;

        public MessageReceiver(IModel channel)
        {
            _channel = channel;
        }

        /// <summary>
        /// Стандартный событийный перегруженный подписчик
        /// Авторизуется в приложении и вызывает в новом потоке методы запуска интеграций в CRM
        /// </summary>
        /// <param name="consumerTag"></param>
        /// <param name="deliveryTag"></param>
        /// <param name="redelivered"></param>
        /// <param name="exchange"></param>
        /// <param name="routingKey"></param>
        /// <param name="properties"></param>
        /// <param name="rabbitMessage"></param>
        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] rabbitMessage)
        {
            ExecuteThreadParameters executeThreadParameters = new ExecuteThreadParameters(
                Encoding.UTF8.GetString(rabbitMessage), _channel, deliveryTag, routingKey, exchange);
            try
            {
                RequestAuthServiceClass requestAuthService = new RequestAuthServiceClass();
                requestAuthService.AuthRequest();
            }
            catch (Exception ex)
            {
                Log.Write(ex);
            }
            Thread executingIntegration = new Thread(executeThreadParameters.ExecuteCrmThread);
            executingIntegration.Start();
        }

        /// <summary>
        /// Класс обработчик сообщений из RabbitMQ
        /// </summary>
        public class ExecuteThreadParameters
        {
            private readonly IModel _channel;
            private readonly string BasicDeliverEvent;
            private readonly ulong deliveryTag;
            private readonly string _routingKey;
            private readonly string _exchange;

            /// <summary>
            /// Конструктор для заполнения полей для последующей обработки сообщения
            /// </summary>
            /// <param name="basicDeliverEventArgs"></param>
            /// <param name="channel"></param>
            /// <param name="delivery"></param>
            public ExecuteThreadParameters(string basicDeliverEventArgs, IModel channel, ulong delivery, string routingKey, string exchange)
            {
                BasicDeliverEvent = basicDeliverEventArgs;
                _channel = channel;
                deliveryTag = delivery;
                _routingKey = routingKey;
                _exchange = exchange;
            }

            /// <summary>
            /// Метод выполняющий запросы в CRM, по маппингу полученного ключа с файлом SettingsIntegration
            /// Параметр для маппинга ключей в конфигурационном файле: CollectionService
            /// </summary>
            internal void ExecuteCrmThread()
            {
                if (!string.IsNullOrEmpty(BasicDeliverEvent))
                {
                    if (_exchange == "exchange1c")
                    {
                        DataIntegration1C executeData1C = JsonConvert.DeserializeObject<DataIntegration1C>(BasicDeliverEvent);
                        RequestApplicationClass requestApplicationClass = new RequestApplicationClass(executeData1C);
                        StartIntegrationCrm(requestApplicationClass);
                    }
                    if (_exchange == "exchangebilling")
                    {
                        ExecuteDataBilling executeDataBilling = JsonConvert.DeserializeObject<ExecuteDataBilling>(BasicDeliverEvent);
                        RequestApplicationClass requestApplicationClass = new RequestApplicationClass(executeDataBilling);
                        StartIntegrationCrm(requestApplicationClass);
                    }
                }
            }

            private void StartIntegrationCrm(RequestApplicationClass requestApplicationClass)
            {
                try
                {
                    object RequestApplicationClass = requestApplicationClass.Request(GeneralSettingRequest.POST);
                    //_channel.BasicAck(deliveryTag, false);
                }
                catch (Exception ex)
                {
                    Log.Write(ex);
                }
            }
        }

    }
}