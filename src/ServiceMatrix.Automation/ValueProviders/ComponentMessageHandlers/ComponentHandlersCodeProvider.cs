﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;


namespace NServiceBusStudio.Automation.ValueProviders.ComponentMessageHandlers
{
    [CLSCompliant(false)]
    [Category("General")]
    [Description("Generate CustomClassBody value and Set Code properties.")]
    [DisplayName("Returns CustomClassBody value and sets AdditionalUsings, Inherits and ClassBody")]
    public class ComponentHandlersCodeProvider : ComponentFromLinkBasedValueProvider
    {
        public override object Evaluate()
        {
            this.Component.AdditionalUsings = this.GenerateAddtionalUsings();
            this.Component.Inherits = this.GenerateInherits();
            this.Component.ClassBody = this.GenerateClassBody();
            this.Component.InterfaceBody = this.GenerateInterfaceBody();
            return this.GenerateCustomClassBody();
        }

        

        private string GenerateAddtionalUsings()
        {
            // Using should include all the message types namespaces without duplicates
            var sb = new StringBuilder();
            var commandsNamespaces = this.Component.Subscribes.ProcessedCommandLinks.Select(cl => cl.CommandReference.Value.Parent.Namespace)
                .Union(this.Component.Publishes.CommandLinks.Select(cl => cl.CommandReference.Value.Parent.Namespace));
            var eventsNamespaces = this.Component.Subscribes.SubscribedEventLinks
                .Where(l => l.EventReference.Value != null) // Ignore generic message handlers
                .Select(el => el.EventReference.Value.Parent.Namespace);
            var messagesNamespaces = this.Component.Subscribes.HandledMessageLinks.Select(ml => ml.MessageReference.Value.Parent.Namespace);

            foreach (var ns in commandsNamespaces.Union(eventsNamespaces).Union(messagesNamespaces).Distinct())
            {
                sb.AppendLine("using " + ns + ";");
            }

            if (this.Component.IsSaga)
            {
                sb.AppendLine("using NServiceBus.Saga;");
            }

            return sb.ToString();
        }

        private IEnumerable<string> typeNames = null;

        private IEnumerable<string> TypeNames
        {
            get
            {
                if (typeNames == null)
                {
                    var commandsTypes = this.Component.Subscribes.ProcessedCommandLinks.Select(cl => cl.CommandReference.Value.CodeIdentifier);
                    var eventsTypes = this.Component.Subscribes.SubscribedEventLinks.Select(el => el.EventReference.Value == null ? "object" : el.EventReference.Value.CodeIdentifier);

                    typeNames = commandsTypes.Union(eventsTypes).Distinct();
                }
                return typeNames;
            }
        }

        private IEnumerable<IMessageLink> messages = null;

        private IEnumerable<IMessageLink> Messages
        {
            get
            {
                if (messages == null)
                {
                    var commandsTypes = this.Component.Subscribes.ProcessedCommandLinks.Select(cl => cl as IMessageLink);
                    var eventsTypes = this.Component.Subscribes.SubscribedEventLinks.Select(el => el as IMessageLink);
                    var messagesTypes = this.Component.Subscribes.HandledMessageLinks.Select(ml => ml as IMessageLink);
                    messages = commandsTypes.Union(eventsTypes).Union(messagesTypes).Distinct();
                }
                return messages;
            }
        }

        private string GenerateInherits()
        {
            // Iherits from 
            var sb = new StringBuilder();
            bool isFirst = true;

            if (this.Component.IsSaga)
            {
                sb.AppendFormat(": Saga<{0}SagaData>", this.Component.CodeIdentifier);
                isFirst = false;
            }

            if (isFirst)
            {
                sb.AppendFormat(": I{0}, NServiceBus.INServiceBusComponent", this.Component.CodeIdentifier);
                isFirst = false;
            }
            else
            {
                sb.AppendFormat(", I{0}, NServiceBus.INServiceBusComponent", this.Component.CodeIdentifier);
            }

            foreach (var message in this.Messages)
            {
                var definition = message.StartsSaga && this.Component.IsSaga ? "IAmStartedByMessages" : "IHandleMessages";
                definition += "<" + message.GetMessageTypeName() + ">";
                if (isFirst)
                {
                    sb.AppendFormat(": {0}", definition);
                    isFirst = false;
                }
                else
                {
                    sb.AppendFormat(", {0}", definition);
                }
            }

            return sb.ToString();
        }

        private string GenerateClassBody()
        {
            var sb = new StringBuilder();
            
            foreach (var typename in this.TypeNames)
            {
                sb.AppendLine();
                sb.AppendLine("		public void Handle(" + typename + " message)");
                sb.AppendLine("		{");

                if (this.Component.IsSaga)
                {
                    sb.AppendLine("			// Store message in Saga Data for later use");
                    sb.AppendLine("			this.Data." + typename + " = message;");
                }

                sb.AppendLine("			// Handle message on partial class");
                sb.AppendLine("			this.HandleImplementation(message);");

                if (this.Component.AutoPublishMessages)
                {
                    foreach (var publishedCommand in this.Component.Publishes.CommandLinks)
                    {
                        sb.AppendLine();
                        sb.AppendLine("			// Auto-publish Command " + publishedCommand.CodeIdentifier);
                        sb.AppendLine("			var " + publishedCommand.CodeIdentifier + " = new " + publishedCommand.GetMessageTypeFullName() + "();");
                        sb.AppendLine("			Configure" + publishedCommand.CodeIdentifier + "(message, " + publishedCommand.CodeIdentifier + ");");
                        sb.AppendLine("			this.Bus.Send(" + publishedCommand.CodeIdentifier + ");");
                    }

                    foreach (var publishedEvent in this.Component.Publishes.EventLinks)
                    {
                        sb.AppendLine();
                        sb.AppendLine("			// Auto-publish Event " + publishedEvent.CodeIdentifier);
                        sb.AppendLine("			var " + publishedEvent.CodeIdentifier + " = new " + publishedEvent.GetMessageTypeFullName() + "();");
                        sb.AppendLine("			Configure" + publishedEvent.CodeIdentifier + "(message, " + publishedEvent.CodeIdentifier + ");");
                        sb.AppendLine("			this.Bus.Publish(" + publishedEvent.CodeIdentifier + ");");
                    }

                    foreach (var processedCommandLink in this.Component.Subscribes.ProcessedCommandLinks.Where(cl => cl.CommandReference.Value.CodeIdentifier == typename &&
                                                                                                                      cl.ProcessedCommandLinkReply != null))
                    {
                        sb.AppendLine();
                        sb.AppendLine("			// Reply message with defined Response");
                        sb.AppendLine("			var response = new " + processedCommandLink.ProcessedCommandLinkReply.GetMessageTypeFullName() + "();");
                        sb.AppendLine("			Configure" + processedCommandLink.ProcessedCommandLinkReply.CodeIdentifier + "(message, response);");
                        sb.AppendLine("			this.Bus.Reply(response);");
                    }

                    
                }

                if (this.Component.IsSaga)
                {
                    sb.AppendLine();
                    sb.AppendLine("			// Check if Saga is Completed ");
                    sb.AppendLine("			CheckIfAllMessagesReceived();");
                }

                sb.AppendLine("		}");
            }

            foreach (var typename in this.TypeNames)
            {
                sb.AppendLine();
                sb.AppendLine("		partial void HandleImplementation(" + typename + " message);");
            

                if (this.Component.AutoPublishMessages)
                {
                    foreach (var publishedCommand in this.Component.Publishes.CommandLinks)
                    {
                        sb.AppendLine();
                        sb.AppendLine("		partial void Configure" + publishedCommand.CodeIdentifier + "(" + typename + " incomingMessage, " + publishedCommand.GetMessageTypeFullName() + " message);");
                    }

                    foreach (var publishedEvent in this.Component.Publishes.EventLinks)
                    {
                        sb.AppendLine();
                        sb.AppendLine("		partial void Configure" + publishedEvent.CodeIdentifier + "(" + typename + " incomingMessage, " + publishedEvent.GetMessageTypeFullName() + " message);");
                    }

                    foreach (var processedCommandLink in this.Component.Subscribes.ProcessedCommandLinks.Where(cl => cl.CommandReference.Value.CodeIdentifier == typename &&
                                                                                                                      cl.ProcessedCommandLinkReply != null))
                    {
                        sb.AppendLine();
                        sb.AppendLine("		partial void Configure" + processedCommandLink.ProcessedCommandLinkReply.CodeIdentifier + "(" + typename + " incomingMessage, " + processedCommandLink.ProcessedCommandLinkReply.GetMessageTypeFullName() + " response);");
                    }
                }

            }

            foreach (var typename in this.Component.Publishes.CommandLinks.Select(c => c.CommandReference.Value.CodeIdentifier))
            {
                sb.AppendLine();
                sb.AppendLine("        public void Send(" + typename + " message)");
                sb.AppendLine("        {");
                sb.AppendLine("            Configure" + typename + "(message);");
                sb.AppendLine("            Bus.Send(message);");
                sb.AppendLine("        }");
            }


            foreach (var typename in this.Component.Publishes.CommandLinks.Select(c => c.CommandReference.Value.CodeIdentifier))
            {
                sb.AppendLine();
                sb.AppendLine("        partial void Configure" + typename + "(" + typename + " message);");
            }


            foreach (var typename in this.Component.Subscribes.HandledMessageLinks.Select(ml => ml.MessageReference.Value.CodeIdentifier))
            {
                sb.AppendLine();
                sb.AppendLine("    public void Handle(" + typename + " message)");
                sb.AppendLine("    {");

                if (this.Component.IsSaga)
                {
                    sb.AppendLine("        // Store message in Saga Data for later use");
                    sb.AppendLine("        this.Data." + typename + " = message;");
                }

                sb.AppendLine();
                sb.AppendLine("        // Handle message on partial class");
                sb.AppendLine("        this.HandleImplementation(message);");
                

                if (this.Component.IsSaga)
                {
                    sb.AppendLine();
                    sb.AppendLine("        // Check if Saga is Completed ");
                    sb.AppendLine("        CheckIfAllMessagesReceived();");
                }

                sb.AppendLine("    }");
            }

            foreach (var typename in this.Component.Subscribes.HandledMessageLinks.Select(ml => ml.MessageReference.Value.CodeIdentifier))
            {
                sb.AppendLine();
                sb.AppendLine("		partial void HandleImplementation(" + typename + " message);");
            }

            // Check to avoid collision with Saga Bus
            if (!this.Component.IsSaga)
            {
                sb.AppendLine();
                sb.AppendLine("        public IBus Bus { get; set; }");
            }

            if (this.Component.IsSaga)
            {
                sb.AppendLine();
                sb.AppendLine("        public void CheckIfAllMessagesReceived()");
                sb.AppendLine("        {");
                sb.AppendLine("            if (" + String.Join (" && ", this.TypeNames.Select(x => "this.Data." + x + " != null")) + ")");
                sb.AppendLine("            {");
                sb.AppendLine("                AllMessagesReceived();");
                sb.AppendLine("            }");
                sb.AppendLine("        }");

                sb.AppendLine();
                sb.AppendLine("        partial void AllMessagesReceived();");
            }

            //if (this.Component.Publishes.CommandLinks.Any(cl => true))
            //{
            //    sb.AppendLine();
            //    sb.AppendLine("        public class " + this.Component.CodeIdentifier + "Registration : INeedInitialization");
            //    sb.AppendLine("        {");
            //    sb.AppendLine("            public void Init()");
            //    sb.AppendLine("            {");
            //    sb.AppendLine("                Configure.Instance.Configurer.ConfigureComponent<" + this.Component.CodeIdentifier + ">(DependencyLifecycle.InstancePerCall);");
            //    sb.AppendLine("            }");
            //    sb.AppendLine("        }");
            //    sb.AppendLine();
            //}


            if (this.Component.IsSaga)
            {
                sb.AppendLine("     }");
                sb.AppendLine();
                sb.AppendLine("     public partial class " + this.Component.CodeIdentifier + "SagaData : IContainSagaData");
                sb.AppendLine("     {");
                sb.AppendLine("           public virtual Guid Id { get; set; }");
                sb.AppendLine("           public virtual string Originator { get; set; }");
                sb.AppendLine("           public virtual string OriginalMessageId { get; set; }");
                sb.AppendLine();

                foreach (var typename in this.TypeNames)
                {
                    sb.AppendLine("           public virtual " + typename + " " + typename + " { get; set; }");
                }
                foreach (var typename in this.Component.Subscribes.HandledMessageLinks.Select(ml => ml.MessageReference.Value.CodeIdentifier))
                {
                    sb.AppendLine("           public virtual " + typename + " " + typename + " { get; set; }");
                }
            }
            return sb.ToString();
        }

        private string GenerateCustomClassBody()
        {
            var sb = new StringBuilder();
            
            foreach (var typename in this.TypeNames)
            {
                sb.AppendLine();
                sb.AppendLine("        partial void HandleImplementation(" + typename + " message)");
                sb.AppendLine("        {");
                sb.AppendLine("            // TODO: " + this.Component.CodeIdentifier + ": Add code to handle the " + typename + " message.");
                sb.AppendLine("            Console.WriteLine(\"" + this.Component.Parent.Parent.InstanceName + " received \" + message.GetType().Name);");
                sb.AppendLine("        }");
            }

            foreach (var publishedCommand in this.Component.Publishes.CommandLinks)
            {
                sb.AppendLine();
                if (publishedCommand.CommandReference.Value != null)
                {
                    sb.AppendLine("        partial void Configure" + publishedCommand.CommandName + "(" + publishedCommand.CommandName + " message)");
                    sb.AppendLine("        {");
                    sb.AppendLine("            // TODO: " + this.Component.CodeIdentifier + ": Add code to configure the " + publishedCommand.CommandName + " message.");
                    sb.AppendLine("        }");
                }

            }

            foreach (var publishedEvent in this.Component.Publishes.EventLinks)
            {
                sb.AppendLine();
                if (publishedEvent.EventReference.Value != null)
                {
                    sb.AppendLine("        // call Bus.Publish<" + publishedEvent.GetMessageTypeFullName() + ">(m => { /* set properties on m in here */ });");
                }
            }

            return sb.ToString();
        }

        private string GenerateInterfaceBody()
        {
            var sb = new StringBuilder();

            foreach (var typename in this.Component.Publishes.CommandLinks.Select(c => c.CommandReference.Value.CodeIdentifier))
            {
                sb.AppendLine();
                sb.AppendLine("        void Send(" + typename + " message);");
            }

            return sb.ToString();
        }
    }
}