﻿namespace AbstractEndpoint
{
    using System;
    using System.Collections.Generic;
    using NServiceBusStudio.Core;
    using NServiceBusStudio;
    using NuPattern.Runtime.ToolkitInterface;

    public interface IAbstractComponentLink : IToolkitInterface
    {
        IAbstractEndpointComponents ParentEndpointComponents { get; }
        IElementReference<IComponent> ComponentReference { get; }
        IEnumerable<IAbstractComponentLink> Siblings { get; }
        Int64 Order { get; set; }
        void SetNextOrderNumber();
    }
}
