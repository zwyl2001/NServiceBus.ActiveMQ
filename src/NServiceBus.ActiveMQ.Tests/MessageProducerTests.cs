﻿namespace NServiceBus.Transports.ActiveMQ.Tests
{
    using System;
    using ActiveMQ.SessionFactories;
    using Apache.NMS;
    using FluentAssertions;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class MessageProducerTests
    {
        private Mock<ISessionFactory> sessionFactoryMock;
        private Mock<IActiveMqMessageMapper> activeMqMessageMapperMock;
        private Mock<IDestinationEvaluator> destinationEvaluatorMock;
        private MessageProducer testee;

        [SetUp]
        public void SetUp()
        {
            sessionFactoryMock = new Mock<ISessionFactory>(MockBehavior.Loose);
            activeMqMessageMapperMock = new Mock<IActiveMqMessageMapper>();
            destinationEvaluatorMock = new Mock<IDestinationEvaluator>();

            testee = new TestableMessageProducer(
                sessionFactoryMock.Object, 
                activeMqMessageMapperMock.Object,
                destinationEvaluatorMock.Object);
        }

        [Test]
        public void WhenSendingASendMessage_OnException_TheSessionIsReleasedAfterwards()
        {
            const string Reason = "TheExceptionReason";
            var sessionMock = SetupCreateSession();
            activeMqMessageMapperMock.Setup(mm => mm.CreateJmsMessage(It.IsAny<TransportMessage>(), sessionMock.Object, It.IsAny<string>()))
                .Throws(new Exception(Reason));

            Action action = () => testee.SendMessage(new TransportMessage(), string.Empty, string.Empty);

            action.ShouldThrow<Exception>(Reason);
            sessionFactoryMock.Verify(sf => sf.Release(sessionMock.Object));
        }

        [Test]
        public void WhenSendingASendMessage_TheSessionIsReleasedAfterwards()
        {
            var sessionMock = SetupCreateSession();

            testee.SendMessage(new TransportMessage(), string.Empty, string.Empty);

            sessionFactoryMock.Verify(sf => sf.Release(sessionMock.Object));
        }

        [Test]
        public void WhenSendingAMessage()
        {
            const string Destination = "TheDestination";
            const string DestinationPrefix = "TheDestinationPrefix";

            var message = new TransportMessage();
            var sessionMock = SetupCreateSession();
            var producerMock = SetupCreateProducer(sessionMock);
            var jmsMessage = SetupCreateJmsMessageFromTransportMessage(message, sessionMock.Object);
            var destination = SetupGetDestination(sessionMock, Destination, DestinationPrefix);

            testee.SendMessage(message, Destination, DestinationPrefix);

            producerMock.Verify(p => p.Send(destination, jmsMessage));
        }

        private IDestination SetupGetDestination(Mock<ISession> sessionMock, string Destination, string DestinationPrefix)
        {
            var destination = new Mock<IDestination>().Object;
            destinationEvaluatorMock.Setup(de => de.GetDestination(sessionMock.Object, Destination, DestinationPrefix))
                .Returns(destination);
            return destination;
        }

        private Mock<ISession> SetupCreateSession()
        {
            var sessionMock = new Mock<ISession>();
            sessionFactoryMock.Setup(c => c.GetSession()).Returns(sessionMock.Object);
            SetupCreateProducer(sessionMock);
            return sessionMock;
        }

        private IMessage SetupCreateJmsMessageFromTransportMessage(TransportMessage message, ISession session)
        {
            var jmsMessage = new Mock<IMessage>().SetupAllProperties().Object;
            jmsMessage.NMSMessageId = Guid.NewGuid().ToString();

            activeMqMessageMapperMock.Setup(m => m.CreateJmsMessage(message, session, TestableMessageProducer.ProducerId)).Returns(jmsMessage);
            return jmsMessage;
        }

        private Mock<IMessageProducer> SetupCreateProducer(Mock<ISession> sessionMock)
        {
            var producerMock = new Mock<IMessageProducer>();
            sessionMock.Setup(s => s.CreateProducer()).Returns(producerMock.Object);
            return producerMock;
        }

        private class TestableMessageProducer : MessageProducer
        {
            public const string ProducerId = "ProducerId";

            public TestableMessageProducer(ISessionFactory sessionFactory, IActiveMqMessageMapper activeMqMessageMapper, IDestinationEvaluator destinationEvaluator)
                : base(sessionFactory, activeMqMessageMapper, destinationEvaluator)
            {
            }

            protected override string GetProducerId(IMessageProducer producer)
            {
                return ProducerId;
            }
        }
    }
}