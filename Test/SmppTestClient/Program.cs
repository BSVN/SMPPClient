#region Namespaces

using BSN.SmppClient;
using BSN.SmppClient.App;

#endregion

namespace SmppTestClient
{
    class SMSControl
    {
        static void Main(string[] args)
        {
            string server = "smscsim.smpp.org";       // IP Address or Name of the server
            short port = 2775;                          // Port
            string shortLongCode = "55555";             // The short or long code for this bind
            string systemId = "tfjqgANlXRQBukM";               // The system id for authentication
            string password = "CnNUqsed";               // The password of authentication
            DataCodings dataCoding = DataCodings.ASCII; // The encoding to use if Default is returned in any PDU or encoding request

            // Create a esme manager to communicate with an ESME
            ESMEManager connectionManager = new ESMEManager("Test",
                                                shortLongCode,
                                                new ESMEManager.CONNECTION_EVENT_HANDLER(ConnectionEventHandler),
                                                new ESMEManager.RECEIVED_MESSAGE_HANDLER(ReceivedMessageHandler),
                                                new ESMEManager.RECEIVED_GENERICNACK_HANDLER(ReceivedGenericNackHandler),
                                                new ESMEManager.SUBMIT_MESSAGE_HANDLER(SubmitMessageHandler),
                                                new ESMEManager.QUERY_MESSAGE_HANDLER(QueryMessageHandler),
                                                new ESMEManager.LOG_EVENT_HANDLER(LogEventHandler),
                                                new ESMEManager.PDU_DETAILS_EVENT_HANDLER(PduDetailsHandler));

            // Bind one single Receiver connection
            connectionManager.AddConnections(1, ConnectionModes.Receiver, server, port, systemId, password, "Receiver", dataCoding);

            // Bind one Transmitter connection
            connectionManager.AddConnections(1, ConnectionModes.Transmitter, server, port, systemId, password, "Transceiver", dataCoding);

            // Accept command input
            bool bQuit = false;

            for (; ; )
            {
                // Hit Enter in the terminal once the binds are up to see this prompt

                Console.WriteLine("Commands");
                Console.WriteLine("send 12223334444 hello jack");
                Console.WriteLine("quit");
                Console.WriteLine("");

                Console.Write("\n#>");

                string? command = Console.ReadLine();
                if (command == null || command.Length == 0)
                    continue;

                switch (command.Split(' ')[0].ToString())
                {
                    case "quit":
                    case "exit":
                        bQuit = true;
                        break;

                    default:
                        ProcessCommand(connectionManager, command);
                        break;
                }

                if (bQuit)
                    break;
            }

            if (connectionManager != null)
            {
                connectionManager.Dispose();
            }
        }

        private static void ProcessCommand(ESMEManager connectionManager, string command)
        {
            string[] parts = command.Split(' ');

            switch (parts[0])
            {
                case "send":
                    SendMessage(connectionManager, command);
                    break;

                case "query":
                    QueryMessage(connectionManager, command);
                    break;
            }
        }

        private static void SendMessage(ESMEManager connectionManager, string command)
        {
            string[] parts = command.Split(' ');
            string phoneNumber = parts[1];
            string message = string.Join(" ", parts, 2, parts.Length - 2);

            // This is set in the Submit PDU to the SMSC
            // If you are responding to a received message, make this the same as the received message
            // Adjusted to ucs2 for supporting Arabic character.
            DataCodings submitDataCoding = DataCodings.UCS2;

            // Use this to encode the message
            // We need to know the actual encoding.
            // Adjusted to ucs2 for supporting Arabic character.
            DataCodings encodeDataCoding = DataCodings.UCS2;

            // There is a default encoding set for each connection. This is used if the encodeDataCoding is Default

            connectionManager.SendMessage(phoneNumber, null, Ton.National, Npi.ISDN, submitDataCoding, encodeDataCoding, message, out SubmitSm submitSm, out SubmitSmResp submitSmResp);
            Console.Write("submitSm:{0}, submitSmResp:{1}, messageId:{2}", submitSm.DestAddr, submitSmResp.Status, submitSmResp.MessageId);
        }

        private static void QueryMessage(ESMEManager connectionManager, string command)
        {
            string[] parts = command.Split(' ');
            string messageId = parts[1];

            QuerySm querySm = connectionManager.SendQuery(messageId);
            Console.WriteLine(querySm.Status.ToString());
        }

        private static void ReceivedMessageHandler(string logKey, MessageTypes messageType, string serviceType, Ton sourceTon, Npi sourceNpi, string shortLongCode, DateTime dateReceived, string phoneNumber, DataCodings dataCoding, string message)
        {
            if (messageType == MessageTypes.SMSCDeliveryReceipt)
            {
                Console.WriteLine("This is the message for the status of delivery");
                Console.WriteLine("MessageType: " + messageType.ToString());
                Console.WriteLine("ReceivedMessageHandler: {0}", message);
            }
            else
            {
                Console.Write("This is normal message");
                Console.WriteLine("MessageType: " + messageType.ToString());
                Console.WriteLine("ReceivedMessageHandler: {0}", message);
            }
        }

        private static void ReceivedGenericNackHandler(string logKey, int sequence)
        {
        }

        private static void SubmitMessageHandler(string logKey, int sequence, string messageId)
        {
            Console.WriteLine("SubmitMessageHandler: {0}", messageId);
        }

        private static void QueryMessageHandler(string logKey, int sequence, string messageId, DateTime finalDate, int messageState, long errorCode)
        {
            Console.WriteLine("QueryMessageHandler: {0} {1} {2}", messageId, finalDate, messageState);
        }

        private static void LogEventHandler(LogEventNotificationTypes logEventNotificationType, string logKey, string shortLongCode, string message)
        {
            Console.WriteLine(message);
        }

        private static void ConnectionEventHandler(string logKey, ConnectionEventTypes connectionEventType, string message)
        {
            Console.WriteLine("ConnectionEventHandler: {0} {1}", connectionEventType, message);
        }

        private static Guid? PduDetailsHandler(string logKey, PduDirectionTypes pduDirectionType, Header pdu, List<PduPropertyDetail> details)
        {
            Guid? pduHeaderId = null;

            try
            {
                // Do not store these
                if ((pdu.Command == CommandSet.EnquireLink) || (pdu.Command == CommandSet.EnquireLinkResp))
                {
                    return null;
                }

                string? connectionString = null; // If null InsertPdu will just log to stdout
                int serviceId = 0;              // Internal Id used to track multiple SMSC systems

                PduApp.InsertPdu(logKey, connectionString, serviceId, pduDirectionType, details, pdu.PduData.BreakIntoDataBlocks(4096), out pduHeaderId);
            }

            catch (Exception exception)
            {
                Console.WriteLine("{0}", exception.Message);
            }

            return pduHeaderId;
        }
    }
}
