﻿using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FunDns
{
    class Program
    {
        static string[] Search;
        static string AcmeChallenge = null;

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("FunDns.exe secondlevel.tld <acmeChallenge>");
                Console.WriteLine("--> Responds to 127-0-0-1.secondlevel.tld with 127.0.0.1");
                Console.WriteLine("--> If an acmeChallenge is provided, it replies to ALL txt requests with this.");
                return;
            }

            Console.WriteLine("Main domain name: " + args[0]);
            Search = args[0].ToLowerInvariant().Split('.');

            if (args.Length > 1)
                AcmeChallenge = args[1];


            var dnsServer = new DnsServer(IPAddress.Any, 8, 8);
            dnsServer.QueryReceived += DnsServer_QueryReceived;
            dnsServer.Start();

            Task.WaitAll(Task.Delay(-1)); // deliberately returning -1
        }

        private static async Task DnsServer_QueryReceived(object sender, QueryReceivedEventArgs eventArgs)
        {
            if (!(eventArgs.Query is DnsMessage))
                return;

            var msg = eventArgs.Query as DnsMessage;
            if (msg == null || msg.Questions.Count != 1)
                return;

            var question = msg.Questions[0];

            if (question.RecordType == RecordType.Txt && AcmeChallenge != null)
            {
                var resp = msg.CreateResponseInstance();
                resp.ReturnCode = ReturnCode.NoError;
                resp.AnswerRecords.Add(new TxtRecord(question.Name, 60, AcmeChallenge));
                eventArgs.Response = resp;
                return;
            }

            if (question.RecordType == RecordType.CAA && AcmeChallenge != null)
            {
                var resp = msg.CreateResponseInstance();
                resp.ReturnCode = ReturnCode.NxDomain;
                eventArgs.Response = resp;
                return;
            }

            if (question.RecordType != RecordType.A)
                return;


            if (Search.Length + 1 > question.Name.Labels.Length)
                return;

            if (!IPAddress.TryParse(question.Name.Labels[0].Replace('-', '.'), out var ip))
                return;

            for (var i = 0; i < Search.Length; i++)
                if (question.Name.Labels[i + 1].ToLowerInvariant() != Search[i])
                    return;

            var domainName = new string[Search.Length + 1];
            domainName[0] = question.Name.Labels[0];
            Array.Copy(Search, 0, domainName, 1, Search.Length);

            var responseName = new string[Search.Length + 1];
            Array.Copy(question.Name.Labels, 0, responseName, 0, responseName.Length);

            DnsMessage response = msg.CreateResponseInstance();
            response.ReturnCode = ReturnCode.NoError;
            response.AnswerRecords.Add(new ARecord(new DomainName(responseName), 300, ip));

            eventArgs.Response = response;
        }
    }
}
