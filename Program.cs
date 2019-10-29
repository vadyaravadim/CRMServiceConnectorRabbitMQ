using System;
using CrmServiceBus.Models;
using RabbitMQ.Client;
using CrmServiceBus.Receiver;

namespace CrmServiceBus
{
    public class Program
    {
        public static void Main()
        {
            try
            {
                ModelAppSettings.LinkToObject = ModelAppSettings.StartSetupSettingApp();
                SubscribeChannel();
            }
            catch (Exception ex)
            {
                Log.Write(ex);
            }
        }

        /// <summary>
        /// Инициализируем новое подключение к RabbitMQ
        /// </summary>
        public static void SubscribeChannel()
        {
            ConnectionFactory factory = new ConnectionFactory
            {
                Uri = new Uri(ModelAppSettings.LinkToObject.RabbitMQAuthUrl)
            };
            CreateConnection(factory);
        }

        /// <summary>
        /// Подключаемся к RabbitMQ
        /// </summary>
        /// <param name="factory">Принимает объект подключения</param>
        private static void CreateConnection(ConnectionFactory factory)
        {
            using (IConnection connection = factory.CreateConnection())
            {
                using (IModel channel = connection.CreateModel())
                {
                    CreatingModelExchangeConsumer(channel);
                }
            }
        }

        private static void CreatingModelExchangeConsumer(IModel channel)
        {
            foreach (ApplicationSettings appSettings in ModelAppSettings.LinkToObject.ApplicationSettings)
            {
                channel.ExchangeDeclare(exchange: appSettings.Exchange, type: appSettings.Type, durable: true);
                channel.QueueBind(queue: appSettings.QueuName, exchange: appSettings.Exchange, routingKey: "");
                MessageReceiver messageReceiver = new MessageReceiver(channel);
                channel.BasicConsume(appSettings.QueuName, false, messageReceiver);
            }
            Console.ReadKey();
        }

    }
}
