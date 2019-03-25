﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Protocol.TestSuites.Kerberos.Adapter;
using Microsoft.Protocols.TestTools;
using Microsoft.Protocols.TestTools.StackSdk.Asn1;
using Microsoft.Protocols.TestTools.StackSdk.Security.Cryptographic;
using Microsoft.Protocols.TestTools.StackSdk.Security.KerberosLib;
using Microsoft.Protocols.TestTools.StackSdk.Security.Pac;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Protocol.TestSuites.Kerberos.TestSuite.KILE
{
    [TestClass]
    public class KileHttpApTest : TraditionTestBase
    {
        [ClassInitialize()]
        public static void ClassInitialize(TestContext testContext)
        {
            TestClassBase.Initialize(testContext);
        }

        [ClassCleanup()]
        public static void ClassCleanup()
        {
            TestClassBase.Cleanup();
        }

        #region Basic
        [TestMethod]
        [Priority(1)]
        [TestCategory(TestCategories.HttpAp)]
        [TestCategory(TestCategories.SingleRealm)]
        [TestCategory(TestCategories.DFL2K8R2)]
        [TestCategory(TestCategories.KilePac)]        
        [Feature(Feature.Kile | Feature.Pac)]
        [ApplicationServer(ApplicationServer.Http)]
        [Description("This test case is designed to verify if the AP supports Channel Binding and AD-AUTH-DATA-AP-OPTIONS authorization data.")]
        public void ChannelBindingSuccess_Http()
        {
            base.Logging();

            client = new KerberosTestClient(this.testConfig.LocalRealm.RealmName,
                this.testConfig.LocalRealm.User[1].Username,
                this.testConfig.LocalRealm.User[1].Password,
                KerberosAccountType.User,
                testConfig.LocalRealm.KDC[0].IPAddress,
                testConfig.LocalRealm.KDC[0].Port,
                testConfig.TransportType,
                testConfig.SupportedOid);

            // Kerberos Proxy Service is used
            if (this.testConfig.UseProxy)
            {
                BaseTestSite.Log.Add(LogEntryKind.Comment, "Initialize KKDCP Client .");
                KKDCPClient proxyClient = new KKDCPClient(proxyClientConfig);
                proxyClient.TargetDomain = this.testConfig.LocalRealm.RealmName;
                client.UseProxy = true;
                client.ProxyClient = proxyClient;
            }

            //Create and send AS request
            KdcOptions options = KdcOptions.FORWARDABLE | KdcOptions.CANONICALIZE | KdcOptions.RENEWABLE;
            client.SendAsRequest(options, null);
            //Recieve preauthentication required error            
            METHOD_DATA methodData;
            KerberosKrbError krbError = client.ExpectPreauthRequiredError(out methodData);

            //Create sequence of PA data            
            string timeStamp = KerberosUtility.CurrentKerberosTime.Value;
            PaEncTimeStamp paEncTimeStamp = new PaEncTimeStamp(timeStamp,
                0,
                client.Context.SelectedEType,
                this.client.Context.CName.Password,
                this.client.Context.CName.Salt);
            PaPacRequest paPacRequest = new PaPacRequest(true);
            Asn1SequenceOf<PA_DATA> seqOfPaData = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { paEncTimeStamp.Data, paPacRequest.Data });
            //Create and send AS request
            client.SendAsRequest(options, seqOfPaData);
            KerberosAsResponse asResponse = client.ExpectAsResponse();
            BaseTestSite.Assert.IsNotNull(asResponse.Response.ticket, "AS response should contain a TGT.");

            //Create and send TGS request
            client.SendTgsRequest(this.testConfig.LocalRealm.WebServer[0].HttpServiceName, options);
            KerberosTgsResponse tgsResponse = client.ExpectTgsResponse();
            BaseTestSite.Assert.IsNotNull(tgsResponse.Response.ticket, "Service ticket should not be null.");

            //AP exchange part
            AdAuthDataApOptions authApOptions = new AdAuthDataApOptions(KerberosConstValue.KERB_AP_OPTIONS_CBT);
            AdIfRelevent adIfRelevent = new AdIfRelevent(new AD_IF_RELEVANT(new AuthorizationDataElement[] { authApOptions.AuthDataElement }));
            AuthorizationData data = new AuthorizationData(new AuthorizationDataElement[] { adIfRelevent.AuthDataElement });

            EncryptionKey subkey = KerberosUtility.GenerateKey(client.Context.SessionKey);
            byte[] token = client.CreateGssApiToken(ApOptions.MutualRequired,
                data,
                subkey,
                ChecksumFlags.GSS_C_MUTUAL_FLAG | ChecksumFlags.GSS_C_INTEG_FLAG);

            BaseTestSite.Log.Add(LogEntryKind.Comment, "Create and send Http request.");
            KerberosApResponse apRep = client.GetApResponseFromToken(SendAndRecieveHttpAp(this.testConfig.LocalRealm.WebServer[0], token));
            BaseTestSite.Log.Add(LogEntryKind.Comment, "Recieve Http response.");
        }
               
        [TestMethod]
        [Priority(1)]
        [TestCategory(TestCategories.HttpAp)]
        [TestCategory(TestCategories.SingleRealm)]
        [TestCategory(TestCategories.DFL2K8R2)]
        [TestCategory(TestCategories.KilePac)]
        [Feature(Feature.Kile | Feature.Pac)]
        [ApplicationServer(ApplicationServer.Http)]
        [Description("This test case is designed to verify if the AP supports KERB-AD-RESTRICTION-ENTRY authorization data.")]
        public void TokenRestrictionDifferentMachineId_Http()
        {
            base.Logging();

            client = new KerberosTestClient(
                this.testConfig.LocalRealm.RealmName,
                this.testConfig.LocalRealm.User[1].Username,
                this.testConfig.LocalRealm.User[1].Password,
                KerberosAccountType.User,
                testConfig.LocalRealm.KDC[0].IPAddress,
                testConfig.LocalRealm.KDC[0].Port,
                testConfig.TransportType,
                testConfig.SupportedOid);

            // Kerberos Proxy Service is used
            if (this.testConfig.UseProxy)
            {
                BaseTestSite.Log.Add(LogEntryKind.Comment, "Initialize KKDCP Client .");
                KKDCPClient proxyClient = new KKDCPClient(proxyClientConfig);
                proxyClient.TargetDomain = this.testConfig.LocalRealm.RealmName;
                client.UseProxy = true;
                client.ProxyClient = proxyClient;
            }

            //Create and send AS request
            KdcOptions options = KdcOptions.FORWARDABLE | KdcOptions.CANONICALIZE | KdcOptions.RENEWABLE;
            client.SendAsRequest(options, null);
            //Recieve preauthentication required error            
            METHOD_DATA methodData;
            KerberosKrbError krbError = client.ExpectPreauthRequiredError(out methodData);

            //Create sequence of PA data
            string timeStamp = KerberosUtility.CurrentKerberosTime.Value;
            PaEncTimeStamp paEncTimeStamp = new PaEncTimeStamp(timeStamp,
                0,
                client.Context.SelectedEType,
                this.client.Context.CName.Password,
                this.client.Context.CName.Salt);
            PaPacRequest paPacRequest = new PaPacRequest(true);
            Asn1SequenceOf<PA_DATA> seqOfPaData = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { paEncTimeStamp.Data, paPacRequest.Data });
            //Create and send AS request
            client.SendAsRequest(options, seqOfPaData);
            KerberosAsResponse asResponse = client.ExpectAsResponse();
            BaseTestSite.Assert.IsNotNull(asResponse.Response.ticket, "AS response should contain a TGT.");

            //Create and send TGS request
            KerbAuthDataTokenRestrictions krbAuthDataTokenRestictions = new KerbAuthDataTokenRestrictions(
                0,
                (uint)LSAP_TOKEN_INFO_INTEGRITY_Flags.FULL_TOKEN,
                (uint)LSAP_TOKEN_INFO_INTEGRITY_TokenIL.High,
                new Guid("00000000000000000000000000000000").ToString().Replace("-", ""));

            AdIfRelevent adIfRelevent = new AdIfRelevent(new AD_IF_RELEVANT(new AuthorizationDataElement[] { krbAuthDataTokenRestictions.AuthDataElement }));
            AuthorizationData data = new AuthorizationData(new AuthorizationDataElement[] { adIfRelevent.AuthDataElement });

            client.SendTgsRequest(this.testConfig.LocalRealm.WebServer[0].HttpServiceName, options, null, null, data);
            BaseTestSite.Log.Add(LogEntryKind.Comment, "Create and send TGS request");
            KerberosTgsResponse tgsResponse = client.ExpectTgsResponse();
            BaseTestSite.Log.Add(LogEntryKind.Comment, "Recieve a TGS response.");

            BaseTestSite.Assert.IsNotNull(tgsResponse.Response.ticket, "Service ticket should not be null.");
            BaseTestSite.Assert.AreEqual(this.testConfig.LocalRealm.WebServer[0].HttpServiceName,
                KerberosUtility.PrincipalName2String(tgsResponse.Response.ticket.sname),
                "Service principal name in service ticket should match expected.");

            EncryptionKey key = testConfig.QueryKey(this.testConfig.LocalRealm.WebServer[0].HttpServiceName, this.testConfig.LocalRealm.RealmName, this.client.Context.SelectedEType);
            tgsResponse.DecryptTicket(key);

            //tgsResponse.DecryptTicket(testConfig.LocalRealm.WebServer[0].Password, testConfig.LocalRealm.WebServer[0].ServiceSalt);
            BaseTestSite.Assert.IsNotNull(tgsResponse.EncPart, "The encrypted part of TGS-REP is decrypted.");
            BaseTestSite.Assert.AreEqual(this.testConfig.LocalRealm.RealmName.ToLower(),
                tgsResponse.TicketEncPart.crealm.Value.ToLower(),
                "Realm name in service ticket encrypted part should match expected.");
            BaseTestSite.Assert.AreEqual(this.testConfig.LocalRealm.User[1].Username,
                KerberosUtility.PrincipalName2String(tgsResponse.TicketEncPart.cname),
                "Realm name in service ticket encrypted part should match expected.");

            //Assert authorization data	
            LinkedList<IAuthDataElement> authDataList = new LinkedList<IAuthDataElement>();
            BaseTestSite.Assert.IsNotNull(tgsResponse.TicketEncPart.authorization_data, "The ticket contains Authorization data.");
            KerbAuthDataTokenRestrictions tokenRestrictions = FindOneInAuthData<KerbAuthDataTokenRestrictions>(tgsResponse.TicketEncPart.authorization_data.Elements);
            BaseTestSite.Assert.IsNotNull(tokenRestrictions, "KerbAuthDataTokenRestrictions is inside the authorization data.");

            krbAuthDataTokenRestictions = new KerbAuthDataTokenRestrictions(
                0,
                (uint)LSAP_TOKEN_INFO_INTEGRITY_Flags.FULL_TOKEN,
                (uint)LSAP_TOKEN_INFO_INTEGRITY_TokenIL.High,
                new Guid("11111111111111111111111111111111").ToString().Replace("-", ""));

            adIfRelevent = new AdIfRelevent(new AD_IF_RELEVANT(new AuthorizationDataElement[] { krbAuthDataTokenRestictions.AuthDataElement }));
            data = new AuthorizationData(new AuthorizationDataElement[] { adIfRelevent.AuthDataElement });

            EncryptionKey subkey = KerberosUtility.GenerateKey(client.Context.SessionKey);
            byte[] token = client.CreateGssApiToken(ApOptions.MutualRequired,
                data,
                subkey,
                ChecksumFlags.GSS_C_MUTUAL_FLAG | ChecksumFlags.GSS_C_INTEG_FLAG);

            BaseTestSite.Log.Add(LogEntryKind.Comment, "Create and send Http request.");
            KerberosApResponse apRep = client.GetApResponseFromToken(SendAndRecieveHttpAp(this.testConfig.LocalRealm.WebServer[0], token));
            BaseTestSite.Log.Add(LogEntryKind.Comment, "Recieve Http response.");
        }

        [TestMethod]
        [Priority(1)]
        [TestCategory(TestCategories.HttpAp)]
        [TestCategory(TestCategories.SingleRealm)]
        [TestCategory(TestCategories.DFL2K8R2)]
        [TestCategory(TestCategories.KilePac)]
        [Feature(Feature.Kile | Feature.Pac)]
        [ApplicationServer(ApplicationServer.Http)]
        [Description("This test case is designed to verify if the AP supports KERB-AD-RESTRICTION-ENTRY authorization data.")]
        public void TokenRestrictionSameMachineId_Http()
        {
            base.Logging();

            client = new KerberosTestClient(
                this.testConfig.LocalRealm.RealmName,
                this.testConfig.LocalRealm.User[1].Username,
                this.testConfig.LocalRealm.User[1].Password,
                KerberosAccountType.User,
                testConfig.LocalRealm.KDC[0].IPAddress,
                testConfig.LocalRealm.KDC[0].Port,
                testConfig.TransportType,
                testConfig.SupportedOid);

            // Kerberos Proxy Service is used
            if (this.testConfig.UseProxy)
            {
                BaseTestSite.Log.Add(LogEntryKind.Comment, "Initialize KKDCP Client .");
                KKDCPClient proxyClient = new KKDCPClient(proxyClientConfig);
                proxyClient.TargetDomain = this.testConfig.LocalRealm.RealmName;
                client.UseProxy = true;
                client.ProxyClient = proxyClient;
            }

            //Create and send AS request
            KdcOptions options = KdcOptions.FORWARDABLE | KdcOptions.CANONICALIZE | KdcOptions.RENEWABLE;
            client.SendAsRequest(options, null);
            //Recieve preauthentication required error            
            METHOD_DATA methodData;
            KerberosKrbError krbError = client.ExpectPreauthRequiredError(out methodData);

            //Create sequence of PA data
            string timeStamp = KerberosUtility.CurrentKerberosTime.Value;
            PaEncTimeStamp paEncTimeStamp = new PaEncTimeStamp(timeStamp,
                0,
                client.Context.SelectedEType,
                this.client.Context.CName.Password,
                this.client.Context.CName.Salt);
            PaPacRequest paPacRequest = new PaPacRequest(true);
            Asn1SequenceOf<PA_DATA> seqOfPaData = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { paEncTimeStamp.Data, paPacRequest.Data });
            //Create and send AS request
            client.SendAsRequest(options, seqOfPaData);
            KerberosAsResponse asResponse = client.ExpectAsResponse();
            BaseTestSite.Assert.IsNotNull(asResponse.Response.ticket, "AS response should contain a TGT.");

            //Create and send TGS request
            KerbAuthDataTokenRestrictions krbAuthDataTokenRestictions = new KerbAuthDataTokenRestrictions(
                0,
                (uint)LSAP_TOKEN_INFO_INTEGRITY_Flags.FULL_TOKEN,
                (uint)LSAP_TOKEN_INFO_INTEGRITY_TokenIL.High,
                new Guid().ToString().Replace("-", ""));

            AdIfRelevent adIfRelevent = new AdIfRelevent(new AD_IF_RELEVANT(new AuthorizationDataElement[] { krbAuthDataTokenRestictions.AuthDataElement }));
            AuthorizationData data = new AuthorizationData(new AuthorizationDataElement[] { adIfRelevent.AuthDataElement });

            client.SendTgsRequest(this.testConfig.LocalRealm.WebServer[0].HttpServiceName, options, null, null, data);
            BaseTestSite.Log.Add(LogEntryKind.Comment, "Create and send TGS request");
            KerberosTgsResponse tgsResponse = client.ExpectTgsResponse();
            BaseTestSite.Log.Add(LogEntryKind.Comment, "Recieve a TGS response.");

            BaseTestSite.Assert.IsNotNull(tgsResponse.Response.ticket, "Service ticket should not be null.");
            BaseTestSite.Assert.AreEqual(this.testConfig.LocalRealm.WebServer[0].HttpServiceName,
                KerberosUtility.PrincipalName2String(tgsResponse.Response.ticket.sname),
                "Service principal name in service ticket should match expected.");

            EncryptionKey key = testConfig.QueryKey(this.testConfig.LocalRealm.WebServer[0].HttpServiceName, this.testConfig.LocalRealm.RealmName, this.client.Context.SelectedEType);
            tgsResponse.DecryptTicket(key);

            //tgsResponse.DecryptTicket(testConfig.LocalRealm.WebServer[0].Password, testConfig.LocalRealm.WebServer[0].ServiceSalt);
            BaseTestSite.Assert.IsNotNull(tgsResponse.EncPart, "The encrypted part of TGS-REP is decrypted.");
            BaseTestSite.Assert.AreEqual(this.testConfig.LocalRealm.RealmName.ToLower(),
                tgsResponse.TicketEncPart.crealm.Value.ToLower(),
                "Realm name in service ticket encrypted part should match expected.");
            BaseTestSite.Assert.AreEqual(this.testConfig.LocalRealm.User[1].Username,
                KerberosUtility.PrincipalName2String(tgsResponse.TicketEncPart.cname),
                "Realm name in service ticket encrypted part should match expected.");

            //Assert authorization data	
            LinkedList<IAuthDataElement> authDataList = new LinkedList<IAuthDataElement>();
            BaseTestSite.Assert.IsNotNull(tgsResponse.TicketEncPart.authorization_data, "The ticket contains Authorization data.");
            KerbAuthDataTokenRestrictions tokenRestrictions = FindOneInAuthData<KerbAuthDataTokenRestrictions>(tgsResponse.TicketEncPart.authorization_data.Elements);
            BaseTestSite.Assert.IsNotNull(tokenRestrictions, "KerbAuthDataTokenRestrictions is inside the authorization data.");

            EncryptionKey subkey = KerberosUtility.GenerateKey(client.Context.SessionKey);
            byte[] token = client.CreateGssApiToken(ApOptions.MutualRequired,
                data,
                subkey,
                ChecksumFlags.GSS_C_MUTUAL_FLAG | ChecksumFlags.GSS_C_INTEG_FLAG);

            BaseTestSite.Log.Add(LogEntryKind.Comment, "Create and send Http request.");
            KerberosApResponse apRep = client.GetApResponseFromToken(SendAndRecieveHttpAp(this.testConfig.LocalRealm.WebServer[0], token));
            BaseTestSite.Log.Add(LogEntryKind.Comment, "Recieve Http response.");
        }

        [TestMethod]
        [Priority(1)]
        [TestCategory(TestCategories.HttpAp)]
        [TestCategory(TestCategories.SingleRealm)]
        [TestCategory(TestCategories.DFL2K8R2)]
        [TestCategory(TestCategories.KilePac)]
        [Feature(Feature.Kile | Feature.Pac)]
        [ApplicationServer(ApplicationServer.Http)]
        [Description("This test case is designed to test if the KDC supports Service Principal Names to identify server in TGS-REQs.")]
        public void ServicePrincipalName_Http()
        {
            base.Logging();

            client = new KerberosTestClient(this.testConfig.LocalRealm.RealmName,
                this.testConfig.LocalRealm.User[1].Username,
                this.testConfig.LocalRealm.User[1].Password,
                KerberosAccountType.User,
                testConfig.LocalRealm.KDC[0].IPAddress,
                testConfig.LocalRealm.KDC[0].Port,
                testConfig.TransportType,
                testConfig.SupportedOid);

            // Kerberos Proxy Service is used
            if (this.testConfig.UseProxy)
            {
                BaseTestSite.Log.Add(LogEntryKind.Comment, "Initialize KKDCP Client .");
                KKDCPClient proxyClient = new KKDCPClient(proxyClientConfig);
                proxyClient.TargetDomain = this.testConfig.LocalRealm.RealmName;
                client.UseProxy = true;
                client.ProxyClient = proxyClient;
            }

            //Create and send AS request
            KdcOptions options = KdcOptions.FORWARDABLE | KdcOptions.CANONICALIZE | KdcOptions.RENEWABLE;
            client.SendAsRequest(options, null);
            //Recieve preauthentication required error            
            METHOD_DATA methodData;
            KerberosKrbError krbError = client.ExpectPreauthRequiredError(out methodData);

            //Create sequence of PA data            
            string timeStamp = KerberosUtility.CurrentKerberosTime.Value;
            PaEncTimeStamp paEncTimeStamp = new PaEncTimeStamp(timeStamp,
                0,
                client.Context.SelectedEType,
                this.client.Context.CName.Password,
                this.client.Context.CName.Salt);
            PaPacRequest paPacRequest = new PaPacRequest(true);
            Asn1SequenceOf<PA_DATA> seqOfPaData = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { paEncTimeStamp.Data, paPacRequest.Data });
            //Create and send AS request
            client.SendAsRequest(options, seqOfPaData);
            KerberosAsResponse asResponse = client.ExpectAsResponse();
            BaseTestSite.Assert.IsNotNull(asResponse.Response.ticket, "AS response should contain a TGT.");

            //Create and send TGS request
            client.SendTgsRequest(this.testConfig.LocalRealm.WebServer[0].HttpServiceName, options);
            KerberosTgsResponse tgsResponse = client.ExpectTgsResponse();

            BaseTestSite.Assert.AreEqual(this.testConfig.LocalRealm.WebServer[0].HttpServiceName,
                KerberosUtility.PrincipalName2String(tgsResponse.Response.ticket.sname),
                "Service principal name in service ticket should match expected.");

            AuthorizationData data = null;
            EncryptionKey subkey = KerberosUtility.GenerateKey(client.Context.SessionKey);
            byte[] token = client.CreateGssApiToken(ApOptions.MutualRequired,
                data,
                subkey,
                ChecksumFlags.GSS_C_MUTUAL_FLAG | ChecksumFlags.GSS_C_INTEG_FLAG);

            BaseTestSite.Log.Add(LogEntryKind.Comment, "Create and send Http request.");
            KerberosApResponse apRep = client.GetApResponseFromToken(SendAndRecieveHttpAp(this.testConfig.LocalRealm.WebServer[0], token)); ;
            BaseTestSite.Log.Add(LogEntryKind.Comment, "Recieve Http response.");
        }

        [TestMethod]
        [Priority(1)]
        [TestCategory(TestCategories.HttpAp)]
        [TestCategory(TestCategories.SingleRealm)]
        [TestCategory(TestCategories.DFL2K8R2)]
        [TestCategory(TestCategories.KilePac)]
        [Feature(Feature.Kile | Feature.Pac)]
        [ApplicationServer(ApplicationServer.Http)]
        [Description("This test case is designed to verify if the KDC and server supports the RestrictedKrbHost service class.")]
        public void RestrictedKrbHost_Http()
        {
            base.Logging();

            client = new KerberosTestClient(this.testConfig.LocalRealm.RealmName,
                this.testConfig.LocalRealm.User[1].Username,
                this.testConfig.LocalRealm.User[1].Password,
                KerberosAccountType.User,
                testConfig.LocalRealm.KDC[0].IPAddress,
                testConfig.LocalRealm.KDC[0].Port,
                testConfig.TransportType,
                testConfig.SupportedOid);

            // Kerberos Proxy Service is used
            if (this.testConfig.UseProxy)
            {
                BaseTestSite.Log.Add(LogEntryKind.Comment, "Initialize KKDCP Client .");
                KKDCPClient proxyClient = new KKDCPClient(proxyClientConfig);
                proxyClient.TargetDomain = this.testConfig.LocalRealm.RealmName;
                client.UseProxy = true;
                client.ProxyClient = proxyClient;
            }

            //Create and send AS request
            KdcOptions options = KdcOptions.FORWARDABLE | KdcOptions.CANONICALIZE | KdcOptions.RENEWABLE;
            client.SendAsRequest(options, null);
            //Recieve preauthentication required error            
            METHOD_DATA methodData;
            KerberosKrbError krbError = client.ExpectPreauthRequiredError(out methodData);

            //Create sequence of PA data            
            string timeStamp = KerberosUtility.CurrentKerberosTime.Value;
            PaEncTimeStamp paEncTimeStamp = new PaEncTimeStamp(timeStamp,
                0,
                client.Context.SelectedEType,
                this.client.Context.CName.Password,
                this.client.Context.CName.Salt);
            PaPacRequest paPacRequest = new PaPacRequest(true);
            Asn1SequenceOf<PA_DATA> seqOfPaData = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { paEncTimeStamp.Data, paPacRequest.Data });
            //Create and send AS request
            client.SendAsRequest(options, seqOfPaData);
            KerberosAsResponse asResponse = client.ExpectAsResponse();
            BaseTestSite.Assert.IsNotNull(asResponse.Response.ticket, "AS response should contain a TGT.");

            //Create and send TGS request
            client.SendTgsRequest(this.testConfig.LocalRealm.WebServer[0].HttpServiceName, options);
            KerberosTgsResponse tgsResponse = client.ExpectTgsResponse();

            BaseTestSite.Assert.AreEqual(this.testConfig.LocalRealm.WebServer[0].HttpServiceName,
                KerberosUtility.PrincipalName2String(tgsResponse.Response.ticket.sname),
                "Service principal name in service ticket should be RestictedKrnHost.");

            string sName = "ResticketedKrbHost" + "/" + this.testConfig.LocalRealm.WebServer[0].FQDN;
            string domain = this.testConfig.LocalRealm.RealmName;
            client.Context.Ticket.Ticket.sname = new PrincipalName(new KerbInt32((int)PrincipalType.NT_SRV_INST), KerberosUtility.String2SeqKerbString(sName, domain));

            AuthorizationData data = null;
            EncryptionKey subkey = KerberosUtility.GenerateKey(client.Context.SessionKey);
            byte[] token = client.CreateGssApiToken(ApOptions.MutualRequired,
                data,
                subkey,
                ChecksumFlags.GSS_C_MUTUAL_FLAG | ChecksumFlags.GSS_C_INTEG_FLAG);

            BaseTestSite.Log.Add(LogEntryKind.Comment, "Create and send Http request.");
            KerberosApResponse apRep = client.GetApResponseFromToken(SendAndRecieveHttpAp(this.testConfig.LocalRealm.WebServer[0], token)); ;
            BaseTestSite.Log.Add(LogEntryKind.Comment, "Recieve Http response.");
        }

        [TestMethod]
        [Priority(1)]
        [TestCategory(TestCategories.HttpAp)]
        [TestCategory(TestCategories.SingleRealm)]
        [TestCategory(TestCategories.DFL2K8R2)]
        [TestCategory(TestCategories.KilePac)]
        [Feature(Feature.Kile | Feature.Pac)]
        [ApplicationServer(ApplicationServer.Http)]
        [Description("This test case is designed to verify if client can request ticket without PAC from KDC.")]
        public void ServiceTicketWithoutPac_Http()
        {
            base.Logging();

            //SUT control adapter, turn off pac
            client = new KerberosTestClient(
                this.testConfig.LocalRealm.RealmName,
                this.testConfig.LocalRealm.User[1].Username,
                this.testConfig.LocalRealm.User[1].Password,
                KerberosAccountType.User,
                testConfig.LocalRealm.KDC[0].IPAddress,
                testConfig.LocalRealm.KDC[0].Port,
                testConfig.TransportType,
                testConfig.SupportedOid);

            // Kerberos Proxy Service is used
            if (this.testConfig.UseProxy)
            {
                BaseTestSite.Log.Add(LogEntryKind.Comment, "Initialize KKDCP Client .");
                KKDCPClient proxyClient = new KKDCPClient(proxyClientConfig);
                proxyClient.TargetDomain = this.testConfig.LocalRealm.RealmName;
                client.UseProxy = true;
                client.ProxyClient = proxyClient;
            }

            //Create and send AS request
            KdcOptions options = KdcOptions.FORWARDABLE | KdcOptions.CANONICALIZE | KdcOptions.RENEWABLE;
            client.SendAsRequest(options, null);
            //Recieve preauthentication required error            
            METHOD_DATA methodData;
            KerberosKrbError krbError = client.ExpectPreauthRequiredError(out methodData);

            //Create sequence of PA data
            string timeStamp = KerberosUtility.CurrentKerberosTime.Value;
            PaEncTimeStamp paEncTimeStamp = new PaEncTimeStamp(timeStamp,
                0,
                client.Context.SelectedEType,
                this.client.Context.CName.Password,
                this.client.Context.CName.Salt);
            PaPacRequest paPacRequest = new PaPacRequest(true);
            Asn1SequenceOf<PA_DATA> seqOfPaData = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { paEncTimeStamp.Data, paPacRequest.Data });
            //Create and send AS request
            client.SendAsRequest(options, seqOfPaData);
            KerberosAsResponse asResponse = client.ExpectAsResponse();
            BaseTestSite.Assert.IsNotNull(asResponse.Response.ticket, "AS response should contain a TGT.");

            //Create and send TGS request
            Adapter.WebServer ServerAuthNotRequired = new Adapter.WebServer();
            ServerAuthNotRequired.HttpServiceName = "http/" + this.testConfig.LocalRealm.AuthNotRequired.FQDN;
            ServerAuthNotRequired.Password = this.testConfig.LocalRealm.AuthNotRequired.Password;
            ServerAuthNotRequired.ServiceSalt = this.testConfig.LocalRealm.AuthNotRequired.ServiceSalt;

            client.SendTgsRequest(ServerAuthNotRequired.HttpServiceName, options);
            KerberosTgsResponse tgsResponse = client.ExpectTgsResponse();

            BaseTestSite.Assert.AreEqual(ServerAuthNotRequired.HttpServiceName,
                KerberosUtility.PrincipalName2String(tgsResponse.Response.ticket.sname),
                "Service principal name in service ticket should match expected.");
            
            tgsResponse.DecryptTicket(ServerAuthNotRequired.Password, ServerAuthNotRequired.ServiceSalt);
            BaseTestSite.Assert.AreEqual(this.testConfig.LocalRealm.RealmName.ToLower(),
                tgsResponse.TicketEncPart.crealm.Value.ToLower(),
                "Realm name in service ticket encrypted part should match expected.");
            BaseTestSite.Assert.AreEqual(this.testConfig.LocalRealm.User[1].Username,
                KerberosUtility.PrincipalName2String(tgsResponse.TicketEncPart.cname),
                "User name in service ticket encrypted part should match expected.");

            //Assert pac not exist
            if (tgsResponse.TicketEncPart.authorization_data != null)
            {
                AdWin2KPac adWin2kPac = FindOneInAuthData<AdWin2KPac>(tgsResponse.TicketEncPart.authorization_data.Elements);
                BaseTestSite.Assert.IsNull(adWin2kPac,
                    "If the Application Server's service account AuthorizationDataNotRequired is set to TRUE, the KDC MUST NOT include a PAC in the service ticket.");
            }
            else
            {
                BaseTestSite.Assert.IsNull(tgsResponse.TicketEncPart.authorization_data, 
                    "If the Application Server's service account AuthorizationDataNotRequired is set to TRUE, the KDC MUST NOT include a PAC in the service ticket.");
            }
        }

        [TestMethod]
        [Priority(1)]
        [TestCategory(TestCategories.HttpAp)]
        [TestCategory(TestCategories.SingleRealm)]
        [TestCategory(TestCategories.DFL2K8R2)]
        [TestCategory(TestCategories.KilePac)]
        [Feature(Feature.Kile | Feature.Pac)]
        [ApplicationServer(ApplicationServer.Http)]
        [Description("This case is designed to verify if application server can detect ticket modification")]
        public void DetectTicketModification_Http()
        {
            base.Logging();

            client = new KerberosTestClient(this.testConfig.LocalRealm.RealmName,
                this.testConfig.LocalRealm.User[1].Username,
                this.testConfig.LocalRealm.User[1].Password,
                KerberosAccountType.User,
                testConfig.LocalRealm.KDC[0].IPAddress,
                testConfig.LocalRealm.KDC[0].Port,
                testConfig.TransportType,
                testConfig.SupportedOid);

            // Kerberos Proxy Service is used
            if (this.testConfig.UseProxy)
            {
                BaseTestSite.Log.Add(LogEntryKind.Comment, "Initialize KKDCP Client .");
                KKDCPClient proxyClient = new KKDCPClient(proxyClientConfig);
                proxyClient.TargetDomain = this.testConfig.LocalRealm.RealmName;
                client.UseProxy = true;
                client.ProxyClient = proxyClient;
            }

            //Create and send AS request
            KdcOptions options = KdcOptions.FORWARDABLE | KdcOptions.CANONICALIZE | KdcOptions.RENEWABLE;
            client.SendAsRequest(options, null);
            //Recieve preauthentication required error            
            METHOD_DATA methodData;
            KerberosKrbError krbError = client.ExpectPreauthRequiredError(out methodData);

            //Create sequence of PA data            
            string timeStamp = KerberosUtility.CurrentKerberosTime.Value;
            PaEncTimeStamp paEncTimeStamp = new PaEncTimeStamp(timeStamp,
                0,
                client.Context.SelectedEType,
                this.client.Context.CName.Password,
                this.client.Context.CName.Salt);
            PaPacRequest paPacRequest = new PaPacRequest(true);
            Asn1SequenceOf<PA_DATA> seqOfPaData = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { paEncTimeStamp.Data, paPacRequest.Data });
            //Create and send AS request
            client.SendAsRequest(options, seqOfPaData);
            KerberosAsResponse asResponse = client.ExpectAsResponse();
            BaseTestSite.Assert.IsNotNull(asResponse.Response.ticket, "AS response should contain a TGT.");

            //Create and send TGS request
            client.SendTgsRequest(this.testConfig.LocalRealm.WebServer[0].HttpServiceName, options);
            KerberosTgsResponse tgsResponse = client.ExpectTgsResponse();


            EncryptionKey tgskey = testConfig.QueryKey(this.testConfig.LocalRealm.WebServer[0].HttpServiceName, this.testConfig.LocalRealm.RealmName, this.client.Context.SelectedEType);
            tgsResponse.DecryptTicket(tgskey);

            //Change ticket
            //tgsResponse.DecryptTicket(this.testConfig.LocalRealm.WebServer[0].Password, this.testConfig.LocalRealm.WebServer[0].ServiceSalt);
            //tgsResponse.TicketEncPart.cname = new PrincipalName((long)PrincipalType.NT_PRINCIPAL, KerberosUtility.String2SeqKerbString("NonExistUser", testConfig.LocalRealm.RealmName));
            Asn1BerEncodingBuffer encodeBuffer = new Asn1BerEncodingBuffer();
            tgsResponse.TicketEncPart.BerEncode(encodeBuffer, true);

            EncryptionType encryptType = (EncryptionType)tgsResponse.Response.ticket.enc_part.etype.Value;
            var key = KeyGenerator.MakeKey(encryptType, "WrongPassword", this.testConfig.LocalRealm.WebServer[0].ServiceSalt);
            var encrypedData = KerberosUtility.Encrypt(
                encryptType,
                key,
                encodeBuffer.Data,
                (int)KeyUsageNumber.AS_REP_TicketAndTGS_REP_Ticket);
            tgsResponse.Response.ticket.enc_part = new EncryptedData(new KerbInt32((long)encryptType), null, new Asn1OctetString(encrypedData));

            AuthorizationData data = null;
            EncryptionKey subkey = KerberosUtility.GenerateKey(client.Context.SessionKey);
            byte[] token = client.CreateGssApiToken(ApOptions.MutualRequired,
                data,
                subkey,
                ChecksumFlags.GSS_C_MUTUAL_FLAG | ChecksumFlags.GSS_C_INTEG_FLAG);

            BaseTestSite.Log.Add(LogEntryKind.Comment, "Create and send Http request.");
            //Receive Error here
            KerberosKrbError error = client.GetKrbErrorFromToken(SendAndRecieveHttpAp(this.testConfig.LocalRealm.WebServer[0], token));
            BaseTestSite.Assert.AreEqual(KRB_ERROR_CODE.KRB_AP_ERR_MODIFIED,
                error.ErrorCode,
                "AP should return KRB_AP_ERR_MODIFIED if authenticator changed");
        }

        [TestMethod]
        [Priority(1)]
        [TestCategory(TestCategories.HttpAp)]
        [TestCategory(TestCategories.SingleRealm)]
        [TestCategory(TestCategories.DFL2K8R2)]
        [TestCategory(TestCategories.KilePac)]
        [Feature(Feature.Kile | Feature.Pac)]
        [ApplicationServer(ApplicationServer.Http)]
        [Description("This case is designed to verify if application server can detect authenticator modification")]
        public void DetectAuthenticatorModification_Http()
        {
            base.Logging();

            client = new KerberosTestClient(this.testConfig.LocalRealm.RealmName,
                this.testConfig.LocalRealm.User[1].Username,
                this.testConfig.LocalRealm.User[1].Password,
                KerberosAccountType.User,
                testConfig.LocalRealm.KDC[0].IPAddress,
                testConfig.LocalRealm.KDC[0].Port,
                testConfig.TransportType,
                testConfig.SupportedOid);

            // Kerberos Proxy Service is used
            if (this.testConfig.UseProxy)
            {
                BaseTestSite.Log.Add(LogEntryKind.Comment, "Initialize KKDCP Client .");
                KKDCPClient proxyClient = new KKDCPClient(proxyClientConfig);
                proxyClient.TargetDomain = this.testConfig.LocalRealm.RealmName;
                client.UseProxy = true;
                client.ProxyClient = proxyClient;
            }

            //Create and send AS request
            KdcOptions options = KdcOptions.FORWARDABLE | KdcOptions.CANONICALIZE | KdcOptions.RENEWABLE;
            client.SendAsRequest(options, null);
            //Recieve preauthentication required error            
            METHOD_DATA methodData;
            KerberosKrbError krbError = client.ExpectPreauthRequiredError(out methodData);

            //Create sequence of PA data            
            string timeStamp = KerberosUtility.CurrentKerberosTime.Value;
            PaEncTimeStamp paEncTimeStamp = new PaEncTimeStamp(timeStamp,
                0,
                client.Context.SelectedEType,
                this.client.Context.CName.Password,
                this.client.Context.CName.Salt);
            PaPacRequest paPacRequest = new PaPacRequest(true);
            Asn1SequenceOf<PA_DATA> seqOfPaData = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { paEncTimeStamp.Data, paPacRequest.Data });
            //Create and send AS request
            client.SendAsRequest(options, seqOfPaData);
            KerberosAsResponse asResponse = client.ExpectAsResponse();
            BaseTestSite.Assert.IsNotNull(asResponse.Response.ticket, "AS response should contain a TGT.");

            //Create and send TGS request
            client.SendTgsRequest(this.testConfig.LocalRealm.WebServer[0].HttpServiceName, options);
            KerberosTgsResponse tgsResponse = client.ExpectTgsResponse();

            //change authenticator
            client.Context.Ticket.SessionKey = KerberosUtility.GenerateKey(client.Context.SessionKey);

            AuthorizationData data = null;
            EncryptionKey subkey = KerberosUtility.GenerateKey(client.Context.SessionKey);
            byte[] token = client.CreateGssApiToken(ApOptions.MutualRequired,
                data,
                subkey,
                ChecksumFlags.GSS_C_MUTUAL_FLAG | ChecksumFlags.GSS_C_INTEG_FLAG);

            BaseTestSite.Log.Add(LogEntryKind.Comment, "Create and send Http request.");
            //Receive Error here
            KerberosKrbError error = client.GetKrbErrorFromToken(SendAndRecieveHttpAp(this.testConfig.LocalRealm.WebServer[0], token));
            BaseTestSite.Assert.AreEqual(KRB_ERROR_CODE.KRB_AP_ERR_MODIFIED, error.ErrorCode, "AP should return KRB_AP_ERR_MODIFIED if authenticator changed");
        }
        #endregion

        #region Claims
        [TestMethod]
        [Priority(1)]
        [TestCategory(TestCategories.HttpAp)]
        [TestCategory(TestCategories.SingleRealm)]
        [TestCategory(TestCategories.DFL2K12)]
        [TestCategory(TestCategories.Claim)]
        [Feature(Feature.Kile | Feature.Pac | Feature.Claim)]
        [ApplicationServer(ApplicationServer.Http)]
        [Description("This test case is designed to verify if client can request ticket with device claim from KDC")]
        public void RequestDeviceClaim_Http()
        {
            base.Logging();

            client = new KerberosTestClient(
                this.testConfig.LocalRealm.RealmName,
                this.testConfig.LocalRealm.ClientComputer.NetBiosName,
                this.testConfig.LocalRealm.ClientComputer.Password,
                KerberosAccountType.Device, testConfig.LocalRealm.KDC[0].IPAddress,
                testConfig.LocalRealm.KDC[0].Port,
                testConfig.TransportType,
                testConfig.SupportedOid,
                testConfig.LocalRealm.ClientComputer.AccountSalt);

            // Kerberos Proxy Service is used
            if (this.testConfig.UseProxy)
            {
                BaseTestSite.Log.Add(LogEntryKind.Comment, "Initialize KKDCP Client .");
                KKDCPClient proxyClient = new KKDCPClient(proxyClientConfig);
                proxyClient.TargetDomain = this.testConfig.LocalRealm.RealmName;
                client.UseProxy = true;
                client.ProxyClient = proxyClient;
            }

            // AS_REQ and KRB-ERROR using device principal
            KdcOptions options = KdcOptions.FORWARDABLE | KdcOptions.CANONICALIZE | KdcOptions.RENEWABLE;
            client.SendAsRequest(options, null);
            METHOD_DATA methodData;
            KerberosKrbError krbError1 = client.ExpectPreauthRequiredError(out methodData);

            // AS_REQ and AS_REP using device principal
            string timeStamp = KerberosUtility.CurrentKerberosTime.Value;
            PaEncTimeStamp paEncTimeStamp = new PaEncTimeStamp(timeStamp, 0, client.Context.SelectedEType, this.client.Context.CName.Password, this.client.Context.CName.Salt);
            Asn1SequenceOf<PA_DATA> seqOfPaData = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { paEncTimeStamp.Data });
            client.SendAsRequest(options, seqOfPaData);
            KerberosAsResponse asResponse = client.ExpectAsResponse();

            // Switch to user principal
            client = new KerberosTestClient(this.testConfig.LocalRealm.RealmName, this.testConfig.LocalRealm.User[2].Username, this.testConfig.LocalRealm.User[2].Password, KerberosAccountType.User, client.Context.Ticket, client.Context.SessionKey,
                testConfig.LocalRealm.KDC[0].IPAddress, testConfig.LocalRealm.KDC[0].Port, testConfig.TransportType,
                testConfig.SupportedOid);

            // FAST armored AS_REQ and KRB-ERROR using user principal
            //Create a "random" key.
            var subkey = KerberosUtility.MakeKey(client.Context.SelectedEType, "Password02!", "this is a salt");

            var fastOptions = new Protocols.TestTools.StackSdk.Security.KerberosV5.Preauth.FastOptions(KerberosUtility.ConvertInt2Flags((int)0));
            var apOptions = ApOptions.None;
            string timeStamp2 = KerberosUtility.CurrentKerberosTime.Value;
            PaFxFastReq paFxFastReq = new PaFxFastReq(null);
            Asn1SequenceOf<PA_DATA> seqOfPaData2 = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { (paFxFastReq.Data) });

            client.SendAsRequestWithFast(options, seqOfPaData2, null, subkey, fastOptions, apOptions);
            KerberosKrbError krbError2 = client.ExpectKrbError();
            BaseTestSite.Assert.AreEqual(KRB_ERROR_CODE.KDC_ERR_PREAUTH_REQUIRED, krbError2.ErrorCode, "Pre-authentication required.");

            // FAST armored AS_REQ and AS_REP using user principal
            var userKey = KerberosUtility.MakeKey(
                client.Context.SelectedEType,
                client.Context.CName.Password,
                client.Context.CName.Salt);
            PaEncryptedChallenge paEncTimeStamp3 = new PaEncryptedChallenge(
                client.Context.SelectedEType,
                KerberosUtility.CurrentKerberosTime.Value,
                0,
                client.Context.FastArmorkey,
                userKey);

            PaPacRequest paPacRequest = new PaPacRequest(true);
            PaPacOptions paPacOptions = new PaPacOptions(PacOptions.Claims | PacOptions.ForwardToFullDc);
            Asn1SequenceOf<PA_DATA> seqOfPaData3 = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { paEncTimeStamp3.Data, paPacRequest.Data, paPacOptions.Data });

            client.SendAsRequestWithFast(options, seqOfPaData3, null, subkey, fastOptions, apOptions);
            KerberosAsResponse userKrbAsRep = client.ExpectAsResponse();
            if (testConfig.IsClaimSupported)
            {
                PaSupportedEncTypes paSupportedEncTypes = null;
                foreach (var padata in userKrbAsRep.EncPart.pa_datas.Elements)
                {
                    var parsedPadata = PaDataParser.ParseRepPaData(padata);
                    if (parsedPadata is PaSupportedEncTypes)
                        paSupportedEncTypes = parsedPadata as PaSupportedEncTypes;
                }

                BaseTestSite.Assert.IsNotNull(paSupportedEncTypes, "The encrypted padata of AS-REP contains PA_SUPPORTED_ENCTYPES.");
                BaseTestSite.Assert.IsTrue(
                    paSupportedEncTypes.SupportedEncTypes.HasFlag(SupportedEncryptionTypes.Claims_Supported),
                    "Claims is supported.");
                BaseTestSite.Assert.IsTrue(
                    paSupportedEncTypes.SupportedEncTypes.HasFlag(SupportedEncryptionTypes.FAST_Supported),
                    "FAST is supported.");
            }
            // FAST armored TGS_REQ and TGS_REP using user principal
            subkey = KerberosUtility.MakeKey(client.Context.SelectedEType, "Password03!", "this is a salt");

            client.Context.ArmorSessionKey = client.Context.Ticket.SessionKey;
            client.Context.ArmorTicket = client.Context.Ticket;

            client.SendTgsRequestWithExplicitFast(testConfig.LocalRealm.WebServer[0].HttpServiceName, options, null, null, subkey, fastOptions, apOptions);
            KerberosTgsResponse userKrbTgsRep = client.
                ExpectTgsResponse(KeyUsageNumber.TGS_REP_encrypted_part_subkey);
            if (testConfig.IsClaimSupported)
            {
                PaSupportedEncTypes paSupportedEncTypes = null;
                BaseTestSite.Assert.IsNotNull(asResponse.EncPart, "The encrypted part of AS-REP is decrypted.");
                BaseTestSite.Assert.IsNotNull(asResponse.EncPart.pa_datas, "The encrypted padata is not null.");
                foreach (var padata in userKrbTgsRep.EncPart.pa_datas.Elements)
                {
                    var parsedPadata = PaDataParser.ParseRepPaData(padata);
                    if (parsedPadata is PaSupportedEncTypes)
                        paSupportedEncTypes = parsedPadata as PaSupportedEncTypes;
                }
                BaseTestSite.Assert.IsNotNull(paSupportedEncTypes, "The encrypted padata of AS-REP contains PA_SUPPORTED_ENCTYPES.");
                BaseTestSite.Assert.IsTrue(
                    paSupportedEncTypes.SupportedEncTypes.HasFlag(SupportedEncryptionTypes.CompoundIdentity_Supported),
                    "Compound identity is supported.");
            }
            
            EncryptionKey key = testConfig.QueryKey(this.testConfig.LocalRealm.WebServer[0].HttpServiceName, this.testConfig.LocalRealm.RealmName, this.client.Context.SelectedEType);
            userKrbTgsRep.DecryptTicket(key);

            //userKrbTgsRep.DecryptTicket(testConfig.LocalRealm.WebServer[0].Password, testConfig.LocalRealm.WebServer[0].ServiceSalt);

            //Verify PAC
            if (testConfig.IsKileImplemented)
            {
                BaseTestSite.Assert.IsNotNull(userKrbTgsRep.TicketEncPart.authorization_data, "The ticket contains Authorization data.");
                AdWin2KPac adWin2kPac = FindOneInAuthData<AdWin2KPac>(userKrbTgsRep.TicketEncPart.authorization_data.Elements);
                BaseTestSite.Assert.IsNotNull(adWin2kPac, "The Authorization data contains AdWin2KPac.");

                DeviceClaimsInfo deviceClaimsInfo = null;
                foreach (var buf in adWin2kPac.Pac.PacInfoBuffers)
                {
                    if (buf is DeviceClaimsInfo)
                    {
                        deviceClaimsInfo = buf as DeviceClaimsInfo;
                        break;
                    }
                }
                BaseTestSite.Assert.IsNotNull(deviceClaimsInfo, "PAC_DEVICE_INFO is generated.");
            }

            AuthorizationData data = null;
            subkey = KerberosUtility.GenerateKey(client.Context.SessionKey);
            byte[] token = client.CreateGssApiToken(ApOptions.MutualRequired,
                data,
                subkey,
                ChecksumFlags.GSS_C_MUTUAL_FLAG | ChecksumFlags.GSS_C_INTEG_FLAG);

            KerberosApResponse apRep = client.GetApResponseFromToken(SendAndRecieveHttpAp(this.testConfig.LocalRealm.WebServer[0], token));
        }

        [TestMethod]
        [Priority(1)]
        [TestCategory(TestCategories.HttpAp)]
        [TestCategory(TestCategories.SingleRealm)]
        [TestCategory(TestCategories.DFL2K12)]
        [TestCategory(TestCategories.Claim)]
        [Feature(Feature.Kile | Feature.Pac | Feature.Claim)]
        [ApplicationServer(ApplicationServer.Http)]
        [Description("This test case is designed to verify if client can request ticket with user claim from KDC")]
        public void RequestUserClaim_Http()
        {
            base.Logging();

            client = new KerberosTestClient(this.testConfig.LocalRealm.RealmName,
                this.testConfig.LocalRealm.User[2].Username,
                this.testConfig.LocalRealm.User[2].Password,
                KerberosAccountType.User,
                testConfig.LocalRealm.KDC[0].IPAddress,
                testConfig.LocalRealm.KDC[0].Port,
                testConfig.TransportType,
                testConfig.SupportedOid);

            // Kerberos Proxy Service is used
            if (this.testConfig.UseProxy)
            {
                BaseTestSite.Log.Add(LogEntryKind.Comment, "Initialize KKDCP Client .");
                KKDCPClient proxyClient = new KKDCPClient(proxyClientConfig);
                proxyClient.TargetDomain = this.testConfig.LocalRealm.RealmName;
                client.UseProxy = true;
                client.ProxyClient = proxyClient;
            }

            //Create and send AS request
            KdcOptions options = KdcOptions.FORWARDABLE | KdcOptions.CANONICALIZE | KdcOptions.RENEWABLE;
            client.SendAsRequest(options, null);
            //Recieve preauthentication required error            
            METHOD_DATA methodData;
            KerberosKrbError krbError = client.ExpectPreauthRequiredError(out methodData);

            //Create sequence of PA data            
            string timeStamp = KerberosUtility.CurrentKerberosTime.Value;
            PaEncTimeStamp paEncTimeStamp = new PaEncTimeStamp(timeStamp,
                0,
                client.Context.SelectedEType,
                this.client.Context.CName.Password,
                this.client.Context.CName.Salt);
            PaPacRequest paPacRequest = new PaPacRequest(true);
            PaPacOptions paPacOptions = new PaPacOptions(PacOptions.Claims | PacOptions.ForwardToFullDc);
            Asn1SequenceOf<PA_DATA> seqOfPaData = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { paEncTimeStamp.Data, paPacRequest.Data, paPacOptions.Data });
            //Create and send AS request
            client.SendAsRequest(options, seqOfPaData);
            KerberosAsResponse asResponse = client.ExpectAsResponse();
            BaseTestSite.Assert.IsNotNull(asResponse.Response.ticket, "AS response should contain a TGT.");

            seqOfPaData = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { paPacRequest.Data, paPacOptions.Data });
            //Create and send TGS request
            client.SendTgsRequest(this.testConfig.LocalRealm.WebServer[0].HttpServiceName, options, seqOfPaData);
            KerberosTgsResponse tgsResponse = client.ExpectTgsResponse();


            EncryptionKey key = testConfig.QueryKey(this.testConfig.LocalRealm.WebServer[0].HttpServiceName, this.testConfig.LocalRealm.RealmName, this.client.Context.SelectedEType);
            tgsResponse.DecryptTicket(key);

            //tgsResponse.DecryptTicket(this.testConfig.LocalRealm.WebServer[0].Password, this.testConfig.LocalRealm.WebServer[0].ServiceSalt);

            //Assert authorization data
            if (this.testConfig.IsKileImplemented)
            {
                BaseTestSite.Assert.IsNotNull(tgsResponse.TicketEncPart.authorization_data, "The ticket contains Authorization data.");
                AdWin2KPac adWin2kPac = FindOneInAuthData<AdWin2KPac>(tgsResponse.TicketEncPart.authorization_data.Elements);
                BaseTestSite.Assert.IsNotNull(adWin2kPac, "The Authorization data contains AdWin2KPac.");

                if (this.testConfig.IsClaimSupported)
                {
                    ClientClaimsInfo clienClaimsInfo = null;
                    BaseTestSite.Assert.IsNotNull(adWin2kPac, "The Authorization data contains AdWin2KPac.");
                    foreach (var buf in adWin2kPac.Pac.PacInfoBuffers)
                    {
                        if (buf is ClientClaimsInfo)
                            clienClaimsInfo = buf as ClientClaimsInfo;
                    }
                    BaseTestSite.Assert.IsNotNull(clienClaimsInfo, "The AdWin2KPac contains ClientClaimsInfo.");
                }
            }

            AuthorizationData data = null;
            EncryptionKey subkey = KerberosUtility.GenerateKey(client.Context.SessionKey);
            byte[] token = client.CreateGssApiToken(ApOptions.MutualRequired,
                data,
                subkey,
                ChecksumFlags.GSS_C_MUTUAL_FLAG | ChecksumFlags.GSS_C_INTEG_FLAG);

            KerberosApResponse apRep = client.GetApResponseFromToken(SendAndRecieveHttpAp(this.testConfig.LocalRealm.WebServer[0], token));
        }

        [TestMethod]
        [Priority(1)]
        [TestCategory(TestCategories.HttpAp)]
        [TestCategory(TestCategories.SingleRealm)]
        [TestCategory(TestCategories.DFL2K12)]
        [TestCategory(TestCategories.FAST)]
        [Feature(Feature.Kile | Feature.Pac | Feature.Claim | Feature.FAST)]
        [ApplicationServer(ApplicationServer.Http)]
        [Description("This test case is designed to verify if client can request ticket via FAST from KDC")]
        public void UsingFAST_Http()
        {
            base.Logging();

            client = new KerberosTestClient(this.testConfig.LocalRealm.RealmName,
               this.testConfig.LocalRealm.ClientComputer.NetBiosName,
               this.testConfig.LocalRealm.ClientComputer.Password,
               KerberosAccountType.Device,
               testConfig.LocalRealm.KDC[0].IPAddress,
               testConfig.LocalRealm.KDC[0].Port,
               testConfig.TransportType,
               testConfig.SupportedOid);

            // Kerberos Proxy Service is used
            if (this.testConfig.UseProxy)
            {
                BaseTestSite.Log.Add(LogEntryKind.Comment, "Initialize KKDCP Client .");
                KKDCPClient proxyClient = new KKDCPClient(proxyClientConfig);
                proxyClient.TargetDomain = this.testConfig.LocalRealm.RealmName;
                client.UseProxy = true;
                client.ProxyClient = proxyClient;
            }

            // AS_REQ and KRB-ERROR using device principal
            KdcOptions options = KdcOptions.FORWARDABLE | KdcOptions.CANONICALIZE | KdcOptions.RENEWABLE;
            client.SendAsRequest(options, null);
            METHOD_DATA methodData;
            KerberosKrbError krbError1 = client.ExpectPreauthRequiredError(out methodData);

            // AS_REQ and AS_REP using device principal
            string timeStamp = KerberosUtility.CurrentKerberosTime.Value;
            PaEncTimeStamp paEncTimeStamp = new PaEncTimeStamp(timeStamp,
                0,
                client.Context.SelectedEType,
                this.client.Context.CName.Password,
                this.client.Context.CName.Salt);
            Asn1SequenceOf<PA_DATA> seqOfPaData = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { paEncTimeStamp.Data });
            client.SendAsRequest(options, seqOfPaData);
            KerberosAsResponse asResponse = client.ExpectAsResponse();

            // Switch to user principal
            client = new KerberosTestClient(this.testConfig.LocalRealm.RealmName,
                this.testConfig.LocalRealm.User[2].Username,
                this.testConfig.LocalRealm.User[2].Password,
                KerberosAccountType.User,
                client.Context.Ticket,
                client.Context.SessionKey,
                testConfig.LocalRealm.KDC[0].IPAddress,
                testConfig.LocalRealm.KDC[0].Port,
                testConfig.TransportType,
                testConfig.SupportedOid);

            // FAST armored AS_REQ and KRB-ERROR using user principal
            //Create a "random" key.
            var subkey = KerberosUtility.MakeKey(client.Context.SelectedEType, "Password02!", "this is a salt");

            var fastOptions = new Protocols.TestTools.StackSdk.Security.KerberosV5.Preauth.FastOptions(KerberosUtility.ConvertInt2Flags((int)0));
            var apOptions = ApOptions.None;
            string timeStamp2 = KerberosUtility.CurrentKerberosTime.Value;
            PaFxFastReq paFxReq = new PaFxFastReq(null);
            Asn1SequenceOf<PA_DATA> seqOfPaData2 = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { (paFxReq.Data) });

            client.SendAsRequestWithFast(options, seqOfPaData2, null, subkey, fastOptions, apOptions);
            KerberosKrbError krbError2 = client.ExpectKrbError();
            BaseTestSite.Assert.AreEqual(KRB_ERROR_CODE.KDC_ERR_PREAUTH_REQUIRED, krbError2.ErrorCode, "Pre-authentication required.");

            // FAST armored AS_REQ and AS_REP using user principal
            var userKey = KerberosUtility.MakeKey(
                client.Context.SelectedEType,
                client.Context.CName.Password,
                client.Context.CName.Salt);
            PaEncryptedChallenge paEncTimeStamp3 = new PaEncryptedChallenge(
                client.Context.SelectedEType,
                KerberosUtility.CurrentKerberosTime.Value,
                0,
                client.Context.FastArmorkey,
                userKey);
            PaPacRequest paPacRequest = new PaPacRequest(true);
            PaPacOptions paPacOptions = new PaPacOptions(PacOptions.Claims | PacOptions.ForwardToFullDc);
            Asn1SequenceOf<PA_DATA> seqOfPaData3 = new Asn1SequenceOf<PA_DATA>(new PA_DATA[] { paEncTimeStamp3.Data, paPacRequest.Data, paPacOptions.Data });

            client.SendAsRequestWithFast(options, seqOfPaData3, null, subkey, fastOptions, apOptions);
            KerberosAsResponse userKrbAsRep = client.ExpectAsResponse();

            // FAST armored TGS_REQ and TGS_REP using user principal
            subkey = KerberosUtility.MakeKey(client.Context.SelectedEType, "Password03!", "this is a salt");
            client.SendTgsRequestWithFast(testConfig.LocalRealm.WebServer[0].HttpServiceName, options, null, null, subkey, fastOptions, apOptions);
            KerberosTgsResponse userKrbTgsRep = client.
                ExpectTgsResponse(KeyUsageNumber.TGS_REP_encrypted_part_subkey);

            AuthorizationData data = null;
            subkey = KerberosUtility.GenerateKey(client.Context.SessionKey);
            byte[] token = client.CreateGssApiToken(ApOptions.MutualRequired,
                data,
                subkey,
                ChecksumFlags.GSS_C_MUTUAL_FLAG | ChecksumFlags.GSS_C_INTEG_FLAG);

            KerberosApResponse apRep = client.GetApResponseFromToken(SendAndRecieveHttpAp(this.testConfig.LocalRealm.WebServer[0], token));
        }
        #endregion
    }
}
