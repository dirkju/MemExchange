using System;
using System.Threading.Tasks;
using Disruptor;
using Disruptor.Dsl;
using MemExchange.Core.Logging;
using MemExchange.Core.SharedDto.ClientToServer;
using MemExchange.Server.Incoming.Logging;
using MemExchange.Server.Processor;

namespace MemExchange.Server.Incoming
{
    public class IncomingMessageQueue : IIncomingMessageQueue
    {
        private int ringbufferSize = (int)Math.Pow(256, 2);
        private readonly ILogger logger;
        private readonly IIncomingMessageProcessor messageProcessor;
        private readonly IPerformanceRecorder performanceRecorder;
        private Disruptor<RingbufferByteArray> messageDisruptor;
        private RingBuffer<RingbufferByteArray> messageRingBuffer;
        
        public IncomingMessageQueue(ILogger logger, IIncomingMessageProcessor messageProcessor, IPerformanceRecorder performanceRecorder)
        {
            this.logger = logger;
            this.messageProcessor = messageProcessor;
            this.performanceRecorder = performanceRecorder;
        }

        public void Start()
        {
            messageDisruptor = new Disruptor<RingbufferByteArray>(() => new RingbufferByteArray(), ringbufferSize, TaskScheduler.Default, ProducerType.Single, new SleepingWaitStrategy());
            
            // Switch to this line to use busy spin input queue
            //messageDisruptor = new Disruptor<RingbufferByteArray>(() => new RingbufferByteArray(), new SingleThreadedClaimStrategy(ringbufferSize), new BusySpinWaitStrategy(), TaskScheduler.Default);
            
            messageDisruptor.HandleEventsWith(messageProcessor).Then(performanceRecorder);
            //messageDisruptor.HandleExceptionsWith(new IncomingMessageQueueErrorHandler());
            messageRingBuffer = messageDisruptor.Start();
            performanceRecorder.Setup(messageRingBuffer, 5000);

            logger.Info("Incoming message queue started.");
        }

        public void Stop()
        {
            messageDisruptor.Halt();
            logger.Info("Incoming message queue stopped.");
        }

        public void Enqueue(byte[] incomingBytes)
        {
            var next = messageRingBuffer.Next();
            var entry = messageRingBuffer[next];
            entry.Set(incomingBytes);
            messageRingBuffer.Publish(next);
        }
    }
}