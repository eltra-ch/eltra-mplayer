using EltraCommon.Contracts.Users;
using EltraConnector.Agent;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TestEltraConnector;
using Xunit;
using Xunit.Abstractions;

namespace TestRadioSure
{
    public class RadioSureTest
    {
        #region Private fields

        private string _host = "https://eltra.ch";
        //private string _host = "http://localhost:5001";

        private readonly ITestOutputHelper _testOutputHelper;

        private AgentConnectorTestData _testData;
        private UserIdentity _identity;
        private AgentConnector _connector;


        #endregion

        #region Constructors

        public RadioSureTest(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        #endregion

        #region Properties

        private AgentConnectorTestData TestData
        {
            get => _testData ?? (_testData = new AgentConnectorTestData(Connector, Identity));
        }

        private UserIdentity Identity
        {
            get => _identity ?? (_identity = CreateUserIdentity());
        }

        private AgentConnector Connector => _connector ?? (_connector = new AgentConnector() { Host = _host });

        #endregion

        private UserIdentity CreateUserIdentity()
        {
            return new UserIdentity()
            {
                Login = Guid.NewGuid().ToString() + "@eltra.ch",
                Password = "123456",
                Name = "Unit test user",
                Role = "developer"
            };
        }

        [Theory]
        [InlineData("Poland")]
        [InlineData("Poland Radio")]
        [InlineData("104.6")]
        [InlineData("bavaria")]
        [InlineData("depeche mode")]
        public async Task SingleShotPerformanceTest(string query)
        {
            //Arrange
            var result = await Connector.SignIn(Identity, true);
            int nodeId = 1;
            string radiosureLogin = "radiosure2@eltra.ch";
            var device = await TestData.GetDevice(1, radiosureLogin, "1234");
            string queryResult = string.Empty;

            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var command = await device.GetCommand("QueryStation");

            command.SetParameterValue("Query", query);

            //Act
            var executeResult = await command.Execute();

            executeResult.GetParameterValue("Result", ref queryResult);

            stopwatch.Stop();

            _testOutputHelper.WriteLine($"execute time = {stopwatch.ElapsedMilliseconds} ms, result = {result}, length = {queryResult.Length}");

            //Assert
            Assert.True(result);
            Assert.True(queryResult.Length > 0, "response with no result");
            Assert.NotNull(executeResult);
        }
    }
}
