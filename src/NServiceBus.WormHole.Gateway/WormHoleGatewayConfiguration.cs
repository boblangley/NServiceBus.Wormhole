﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;

namespace NServiceBus.WormHole.Gateway
{
    /// <summary>
    /// Configures the worm hole gateway.
    /// </summary>
    /// <typeparam name="TLocalTransport">Local transport for this site.</typeparam>
    /// <typeparam name="TWormHoleTransport">Worm hole tunnel transport (transports messages between sites).</typeparam>
    public class WormHoleGatewayConfiguration<TLocalTransport, TWormHoleTransport>
        where TLocalTransport : TransportDefinition, new()
        where TWormHoleTransport : TransportDefinition, new()
    {
        string localQueueName;
        string site;

        List<RoutingTableEntry> routes = new List<RoutingTableEntry>();
        Dictionary<string, string> sites = new Dictionary<string, string>();

        Action<RawEndpointConfiguration, TransportExtensions<TLocalTransport>> localTransportCustomization = (c, t) => { };
        Action<RawEndpointConfiguration, TransportExtensions<TWormHoleTransport>> tunnelTransportCustomization = (c, t) => { };

        /// <summary>
        /// Routing table.
        /// </summary>
        public RoutingTable RoutingTable { get; } = new RoutingTable();

        /// <summary>
        /// Endpoint instances.
        /// </summary>
        public EndpointInstances EndpointInstances { get; } = new EndpointInstances();

        /// <summary>
        /// Distribution policy.
        /// </summary>
        public DistributionPolicy DistributionPolicy { get; } = new DistributionPolicy();

        /// <summary>
        /// Gets or sets the error queue.
        /// </summary>
        public string ErrorQueue { get; set; } = "error";

        /// <summary>
        /// Creates new instance.
        /// </summary>
        /// <param name="localQueueName">Name of the queue which is local entry to the tunnel.</param>
        /// <param name="site">Name of this site.</param>
        public WormHoleGatewayConfiguration(string localQueueName, string site)
        {
            this.localQueueName = localQueueName;
            this.site = site;
        }

        /// <summary>
        /// Registers a callback for customizing the local transport.
        /// </summary>
        public void CustomizeLocalTransport(Action<RawEndpointConfiguration, TransportExtensions<TLocalTransport>> customization)
        {
            if (customization == null)
            {
                throw new ArgumentNullException(nameof(customization));
            }
            localTransportCustomization = customization;
        }

        /// <summary>
        /// Registers a callback for customizing the worm hole tunnel transport.
        /// </summary>
        public void CustomizeWormHoleTransport(Action<RawEndpointConfiguration, TransportExtensions<TWormHoleTransport>> customization)
        {
            if (customization == null)
            {
                throw new ArgumentNullException(nameof(customization));
            }
            tunnelTransportCustomization = customization;
        }

        /// <summary>
        /// Configures destination transport address for a given remote site.
        /// </summary>
        /// <param name="remoteSite">Site</param>
        /// <param name="destinationAddress">Destination address.</param>
        public void ConfigureRemoteSite(string remoteSite, string destinationAddress)
        {
            if (remoteSite == null)
            {
                throw new ArgumentNullException(nameof(remoteSite));
            }
            if (destinationAddress == null)
            {
                throw new ArgumentNullException(nameof(destinationAddress));
            }
            sites[remoteSite] = destinationAddress;
        }

        /// <summary>
        /// Instructs the gateway to forward messages of a given type to a given endpoint.
        /// </summary>
        /// <param name="messageType">The message which should be forwarded.</param>
        /// <param name="destination">The destination endpoint.</param>
        public void ForwardToEndpoint(MessageType messageType, string destination)
        {
            ThrowOnAddress(destination);
            routes.Add(new RoutingTableEntry(messageType, UnicastRoute.CreateFromEndpointName(destination)));
        }

        /// <summary>
        /// Instructs the gateway to forward messages from a given assembly to a given endpoint.
        /// </summary>
        /// <param name="assembly">The assembly whose messages should be forwarded.</param>
        /// <param name="destination">Destination endpoint.</param>
        public void ForwardToEndpoint(string assembly, string destination)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            ThrowOnAddress(destination);

            routes.Add(new RoutingTableEntry(new MessageTypeRange(assembly, null), UnicastRoute.CreateFromEndpointName(destination)));
        }

        /// <summary>
        /// Instructs the gateway to forward messages from a given assembly and namespace to a given endpoint.
        /// </summary>
        /// <param name="assembly">The assembly whose messages should be routed.</param>
        /// <param name="namespace">The namespace of the messages which should be forwarded. The given value must exactly match the target namespace.</param>
        /// <param name="destination">Destination endpoint.</param>
        public void ForwardToEndpoint(string assembly, string @namespace, string destination)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }
            if (@namespace == null)
            {
                throw new ArgumentNullException(nameof(@namespace));
            }
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            ThrowOnAddress(destination);

            routes.Add(new RoutingTableEntry(new MessageTypeRange(assembly, @namespace), UnicastRoute.CreateFromEndpointName(destination)));
        }

        /// <summary>
        /// Builds a worm hole gateway instance.
        /// </summary>
        public IStartableWormHoleGateway Build()
        {
            RoutingTable.AddOrReplaceRoutes("StaticConfiguration", routes);

            var router = new MessageRouter(RoutingTable, EndpointInstances, DistributionPolicy);

            return new WormHoleGateway<TLocalTransport, TWormHoleTransport>(localQueueName, 
                new TunnelMessageHandler(router), 
                new SiteMessageHandler(site, sites), 
                ErrorQueue, localTransportCustomization, tunnelTransportCustomization);
        }

        /// <summary>
        /// Builds and starta w worm hole gateway instance.
        /// </summary>
        public Task<IWormHoleGateway> Start()
        {
            var gatway = Build();
            return gatway.Start();
        }

        static void ThrowOnAddress(string destination)
        {
            if (destination.Contains("@"))
            {
                throw new ArgumentException($"A logical endpoint name should not contain '@', but received '{destination}'. To specify an endpoint's address, use the instance mapping file for the MSMQ transport, or refer to the routing documentation.");
            }
        }
    }
}