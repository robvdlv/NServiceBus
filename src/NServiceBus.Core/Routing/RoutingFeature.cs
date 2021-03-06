﻿namespace NServiceBus.Features
{
    using System.Linq;
    using NServiceBus.Config;
    using NServiceBus.Routing.StorageDrivenPublishing;
    using NServiceBus.TransportDispatch;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Messages;
    using NServiceBus.Unicast.Routing;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

    class RoutingFeature : Feature
    {
        public RoutingFeature()
        {
            EnableByDefault();
        }
        protected internal override void Setup(FeatureConfigurationContext context)
        {
            context.Pipeline.Register("DetermineRoutingForMessage", typeof(DetermineRoutingForMessageBehavior), "Determines how the message being sent should be routed");

            var router = SetupStaticRouter(context);
            context.Container.RegisterSingleton(router);

            context.Container.ConfigureComponent(b => new DetermineRoutingForMessageBehavior(context.Settings.LocalAddress(),
                new RoutingAdapter(router)), DependencyLifecycle.InstancePerCall);

            if (!context.Settings.Get<TransportDefinition>().HasNativePubSubSupport)
            {
                context.Container.ConfigureComponent<DispatchStrategy>(b=>new StorageDrivenDispatcher(b.Build<ISubscriptionStorage>(),b.Build<MessageMetadataRegistry>()), DependencyLifecycle.SingleInstance);
            }
            else
            {
                context.Container.ConfigureComponent<DispatchStrategy>(b=>new DefaultDispatchStrategy(),  DependencyLifecycle.SingleInstance);
            }
        }

        static StaticMessageRouter SetupStaticRouter(FeatureConfigurationContext context)
        {
            var conventions = context.Settings.Get<Conventions>();

            var knownMessages = context.Settings.GetAvailableTypes()
                .Where(conventions.IsMessageType)
                .ToList();

            var unicastConfig = context.Settings.GetConfigSection<UnicastBusConfig>();
            var router = new StaticMessageRouter(knownMessages);

            if (unicastConfig != null)
            {
                var messageEndpointMappings = unicastConfig.MessageEndpointMappings.Cast<MessageEndpointMapping>()
                    .OrderByDescending(m => m)
                    .ToList();

                foreach (var mapping in messageEndpointMappings)
                {
                    mapping.Configure((messageType, address) =>
                    {
                        if (!(conventions.IsMessageType(messageType) || conventions.IsEventType(messageType) || conventions.IsCommandType(messageType)))
                        {
                            return;
                        }

                        if (conventions.IsEventType(messageType))
                        {
                            router.RegisterEventRoute(messageType, address);
                            return;
                        }

                        router.RegisterMessageRoute(messageType, address);
                    });
                }
            }

            return router;
        }
    }
}