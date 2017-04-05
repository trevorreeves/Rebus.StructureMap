﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Bus.Advanced;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;
using Rebus.Transport;
using StructureMap;
#pragma warning disable 1998

namespace Rebus.StructureMap
{
    /// <summary>
    /// Implementation of <see cref="IContainerAdapter"/> that uses StructureMap to do its thing
    /// </summary>
    public class StructureMapContainerAdapter : IContainerAdapter
    {
        readonly IContainer _container;

        /// <summary>
        /// Constructs the container adapter
        /// </summary>
        public StructureMapContainerAdapter(IContainer container)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            _container = container;
        }

        /// <summary>
        /// Returns all relevant handler instances for the given message
        /// </summary>
        public async Task<IEnumerable<IHandleMessages<TMessage>>> GetHandlers<TMessage>(TMessage message, ITransactionContext transactionContext)
        {
            var container = transactionContext.GetOrAdd("nested-structuremap-container", () =>
            {
                var nestedContainer = _container.GetNestedContainer();
                transactionContext.OnDisposed(() => nestedContainer.Dispose());
                return nestedContainer;
            });

            var handledMessageTypes = typeof(TMessage).GetBaseTypes()
                .Concat(new[] {typeof(TMessage)});

            var instances = handledMessageTypes
                .SelectMany(handledMessageType =>
                {
                    var implementedInterface = typeof(IHandleMessages<>).MakeGenericType(handledMessageType);

                    return container.GetAllInstances(implementedInterface).Cast<IHandleMessages>();
                })
                .Cast<IHandleMessages<TMessage>>()
                .Distinct()
                .ToList();

            return instances;
        }

        /// <summary>
        /// Sets the bus instance that this <see cref="IContainerAdapter"/> should be able to inject when resolving handler instances
        /// </summary>
        public void SetBus(IBus bus)
        {
            _container.Configure(x =>
            {
                x.For<IBus>().Singleton().Add(bus);
                x.For<ISyncBus>().Transient().Use(c => c.GetInstance<IBus>().Advanced.SyncBus);
                x.For<IMessageContext>().Transient().Use(() => MessageContext.Current);
            });
        }
    }
}
