﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.WormHole;
using NServiceBus.WormHole.Gateway;

namespace Demo
{
    class Program
    {
        const string SqlConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;";

        static void Main(string[] args)
        {
            Start().GetAwaiter().GetResult();
        }

        static async Task Start()
        {
            var endpointA = await StartEndpointA();
            var endpointB = await StartEndpointB();
            var gatewayA = await StartGatewayA();
            var gatewayB = await StartGatewayB();

            while (true)
            {
                Console.WriteLine("Press <enter> to send a message");
                Console.ReadLine();

                await endpointA.Send(new MyMessage());
            }
        }

        static Task<IWormHoleGateway> StartGatewayB()
        {
            var config = new WormHoleGatewayConfiguration<MsmqTransport, RabbitMQTransport>("WormHole-B", "SiteB");
            config.CustomizeWormHoleTransport(ConfigureWormHoleTransport);
            config.ConfigureSite("SiteA", "WormHole-A");
            config.CustomizeLocalTransport((c, t) =>
            {
                c.AutoCreateQueue();
            });
            config.ForwardToEndpoint(new MessageType("MyMessage", "Demo", "Demo"), "EndpointB");

            return config.Start();
        }

        static Task<IWormHoleGateway> StartGatewayA()
        {
            var config = new WormHoleGatewayConfiguration<SqlServerTransport, RabbitMQTransport>("WormHole-A", "SiteA");
            config.CustomizeWormHoleTransport(ConfigureWormHoleTransport);
            config.ConfigureSite("SiteB", "WormHole-B");
            config.CustomizeLocalTransport((c, t) =>
            {
                t.GetSettings().Set<EndpointInstances>(config.EndpointInstances); //SQL transport requires this :(
                t.ConnectionString(SqlConnectionString);
                c.AutoCreateQueue();
            });

            return config.Start();
        }

        static void ConfigureWormHoleTransport(RawEndpointConfiguration c, TransportExtensions<RabbitMQTransport> t)
        {
            t.ConnectionString("host=localhost");
            c.AutoCreateQueue();
        }

        static async Task<IEndpointInstance> StartEndpointB()
        {
            var config = PrepareCommonConfig("EndpointB");
            config.UseTransport<MsmqTransport>();

            return await Endpoint.Start(config);
        }

        static async Task<IEndpointInstance> StartEndpointA()
        {
            var config = PrepareCommonConfig("EndpointA");
            config.UseTransport<SqlServerTransport>().ConnectionString(SqlConnectionString);

            var wormHoleSettings = config.UseWormHoleGateway("WormHole-A");
            wormHoleSettings.RouteToSites(typeof(MyMessage), "SiteB");

            return await Endpoint.Start(config);
        }

        static EndpointConfiguration PrepareCommonConfig(string name)
        {
            var endpointAConfig = new EndpointConfiguration(name);
            endpointAConfig.UsePersistence<InMemoryPersistence>();
            endpointAConfig.SendFailedMessagesTo("error");
            return endpointAConfig;
        }
    }
}