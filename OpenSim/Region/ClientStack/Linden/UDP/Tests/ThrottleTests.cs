/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using NUnit.Framework;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

namespace OpenSim.Region.ClientStack.LindenUDP.Tests
{
    [TestFixture]
    public class ThrottleTests : OpenSimTestCase
    {
        [TestFixtureSetUp]
        public void FixtureInit()
        {
            // Don't allow tests to be bamboozled by asynchronous events.  Execute everything on the same thread.
            Util.FireAndForgetMethod = FireAndForgetMethod.RegressionTest;
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            // We must set this back afterwards, otherwise later tests will fail since they're expecting multiple
            // threads.  Possibly, later tests should be rewritten so none of them require async stuff (which regression
            // tests really shouldn't).
            Util.FireAndForgetMethod = Util.DefaultFireAndForgetMethod;
        }

        [Test]
        public void TestClientThrottleSetNoLimit()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            Scene scene = new SceneHelpers().SetupScene();
            TestLLUDPServer udpServer = ClientStackHelpers.AddUdpServer(scene);

            ScenePresence sp 
                = ClientStackHelpers.AddChildClient(
                    scene, udpServer, TestHelpers.ParseTail(0x1), TestHelpers.ParseTail(0x2), 123456);

            LLUDPClient udpClient = ((LLClientView)sp.ControllingClient).UDPClient;
//            udpClient.ThrottleDebugLevel = 1;

            int resendBytes = 1000;
            int landBytes = 2000;
            int windBytes = 3000;
            int cloudBytes = 4000;
            int taskBytes = 5000;
            int textureBytes = 6000;
            int assetBytes = 7000;

            SetThrottles(
                udpClient, resendBytes, landBytes, windBytes, cloudBytes, taskBytes, textureBytes, assetBytes);

            ClientInfo ci = udpClient.GetClientInfo();

            // We expect this to be lower because of the minimum bound set by MTU
            float totalBytes = LLUDPServer.MTU + landBytes + windBytes + cloudBytes + taskBytes + textureBytes + assetBytes;
            Assert.AreEqual(LLUDPServer.MTU, ci.resendThrottle);
            Assert.AreEqual(landBytes, ci.landThrottle);
            Assert.AreEqual(windBytes, ci.windThrottle);
            Assert.AreEqual(cloudBytes, ci.cloudThrottle);
            Assert.AreEqual(taskBytes, ci.taskThrottle);
            Assert.AreEqual(textureBytes, ci.textureThrottle);
            Assert.AreEqual(assetBytes, ci.assetThrottle);
            Assert.AreEqual(totalBytes, ci.totalThrottle);
        }

        /// <summary>
        /// Test throttle setttings where max client throttle has been limited server side.
        /// </summary>
        [Test]
        public void TestClientThrottleRegionLimited()
        {
            TestHelpers.InMethod();
            //            TestHelpers.EnableLogging();

            int resendBytes = 4000;
            int landBytes = 6000;
            int windBytes = 8000;
            int cloudBytes = 10000;
            int taskBytes = 12000;
            int textureBytes = 14000;
            int assetBytes = 16000;
            int totalBytes 
                = (int)((resendBytes + landBytes + windBytes + cloudBytes + taskBytes + textureBytes + assetBytes) / 2);

            Scene scene = new SceneHelpers().SetupScene();
            TestLLUDPServer udpServer = ClientStackHelpers.AddUdpServer(scene);
            udpServer.Throttle.RequestedDripRate = totalBytes;

            ScenePresence sp 
                = ClientStackHelpers.AddChildClient(
                    scene, udpServer, TestHelpers.ParseTail(0x1), TestHelpers.ParseTail(0x2), 123456);

            LLUDPClient udpClient = ((LLClientView)sp.ControllingClient).UDPClient;
            //            udpClient.ThrottleDebugLevel = 1;

            SetThrottles(
                udpClient, resendBytes, landBytes, windBytes, cloudBytes, taskBytes, textureBytes, assetBytes);

            ClientInfo ci = udpClient.GetClientInfo();

            //            Console.WriteLine(
            //                "Resend={0}, Land={1}, Wind={2}, Cloud={3}, Task={4}, Texture={5}, Asset={6}, TOTAL = {7}", 
            //                ci.resendThrottle, ci.landThrottle, ci.windThrottle, ci.cloudThrottle, ci.taskThrottle, ci.textureThrottle, ci.assetThrottle, ci.totalThrottle);

            Assert.AreEqual(resendBytes / 2, ci.resendThrottle);
            Assert.AreEqual(landBytes / 2, ci.landThrottle);
            Assert.AreEqual(windBytes / 2, ci.windThrottle);
            Assert.AreEqual(cloudBytes / 2, ci.cloudThrottle);
            Assert.AreEqual(taskBytes / 2, ci.taskThrottle);
            Assert.AreEqual(textureBytes / 2, ci.textureThrottle);
            Assert.AreEqual(assetBytes / 2, ci.assetThrottle);
            Assert.AreEqual(totalBytes, ci.totalThrottle);
        }

        /// <summary>
        /// Test throttle setttings where max client throttle has been limited server side.
        /// </summary>
        [Test]
        public void TestClientThrottlePerClientLimited()
        {
            TestHelpers.InMethod();
            //            TestHelpers.EnableLogging();

            int resendBytes = 4000;
            int landBytes = 6000;
            int windBytes = 8000;
            int cloudBytes = 10000;
            int taskBytes = 12000;
            int textureBytes = 14000;
            int assetBytes = 16000;
            int totalBytes 
                = (int)((resendBytes + landBytes + windBytes + cloudBytes + taskBytes + textureBytes + assetBytes) / 2);

            Scene scene = new SceneHelpers().SetupScene();
            TestLLUDPServer udpServer = ClientStackHelpers.AddUdpServer(scene);
            udpServer.ThrottleRates.Total = totalBytes;

            ScenePresence sp 
                = ClientStackHelpers.AddChildClient(
                    scene, udpServer, TestHelpers.ParseTail(0x1), TestHelpers.ParseTail(0x2), 123456);

            LLUDPClient udpClient = ((LLClientView)sp.ControllingClient).UDPClient;
            //            udpClient.ThrottleDebugLevel = 1;

            SetThrottles(
                udpClient, resendBytes, landBytes, windBytes, cloudBytes, taskBytes, textureBytes, assetBytes);

            ClientInfo ci = udpClient.GetClientInfo();

//            Console.WriteLine(
//                "Resend={0}, Land={1}, Wind={2}, Cloud={3}, Task={4}, Texture={5}, Asset={6}, TOTAL = {7}", 
//                ci.resendThrottle, ci.landThrottle, ci.windThrottle, ci.cloudThrottle, ci.taskThrottle, ci.textureThrottle, ci.assetThrottle, ci.totalThrottle);

            Assert.AreEqual(resendBytes / 2, ci.resendThrottle);
            Assert.AreEqual(landBytes / 2, ci.landThrottle);
            Assert.AreEqual(windBytes / 2, ci.windThrottle);
            Assert.AreEqual(cloudBytes / 2, ci.cloudThrottle);
            Assert.AreEqual(taskBytes / 2, ci.taskThrottle);
            Assert.AreEqual(textureBytes / 2, ci.textureThrottle);
            Assert.AreEqual(assetBytes / 2, ci.assetThrottle);
            Assert.AreEqual(totalBytes, ci.totalThrottle);
        }

        private void SetThrottles(
            LLUDPClient udpClient, int resendBytes, int landBytes, int windBytes, int cloudBytes, int taskBytes, int textureBytes, int assetBytes)
        {
            byte[] throttles = new byte[28];

            Array.Copy(BitConverter.GetBytes((float)resendBytes * 8), 0, throttles, 0, 4);
            Array.Copy(BitConverter.GetBytes((float)landBytes * 8), 0, throttles, 4, 4);
            Array.Copy(BitConverter.GetBytes((float)windBytes * 8), 0, throttles, 8, 4);
            Array.Copy(BitConverter.GetBytes((float)cloudBytes * 8), 0, throttles, 12, 4);
            Array.Copy(BitConverter.GetBytes((float)taskBytes * 8), 0, throttles, 16, 4);
            Array.Copy(BitConverter.GetBytes((float)textureBytes * 8), 0, throttles, 20, 4);
            Array.Copy(BitConverter.GetBytes((float)assetBytes * 8), 0, throttles, 24, 4);

            udpClient.SetThrottles(throttles);
        }
    }
}