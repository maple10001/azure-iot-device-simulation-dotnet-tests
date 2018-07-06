using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using Helpers.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DeviceSimulation
{
    public class SimulationTests
    {
        private readonly IHttpClient httpClient;
        private const string DS_ADDRESS = "http://127.0.0.1:9003/v1";

        public SimulationTests()
        {
            this.httpClient = new HttpClient();
        }

        /// <summary>
        /// Integration test using a real HTTP instance.
        /// Test that the service starts normally and returns ok status
        /// </summary>
        [Fact]
        public void Should_Return_OK_Status()
        {
            // Act
            var request = new HttpRequest(DS_ADDRESS + "/status");
            request.AddHeader("X-Foo", "Bar");
            var response = this.httpClient.GetAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        /// <summary>
        /// Simulation service should return a list of simulations
        /// </summary>
        [Fact]
        public void Should_Return_A_List_Of_Simulations()
        {
            // Act
            var request = new HttpRequest(DS_ADDRESS + "/simulations");
            request.AddHeader("X-Foo", "Bar");
            var response = this.httpClient.GetAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            JObject jsonResponse = JObject.Parse(response.Content);

            Assert.True(jsonResponse.HasValues);

            JArray items = (JArray)jsonResponse["Items"];
            
            Assert.True(items.Count >= 0);
        }

        /// <summary>
        /// Simulation service should return a list of device models
        /// </summary>
        [Fact]
        public void Should_Return_A_List_Of_DeviceModels()
        {
            // Arrange
            const int STOCKMODELS = 10;

            // Act
            var request = new HttpRequest(DS_ADDRESS + "/devicemodels");
            request.AddHeader("X-Foo", "Bar");
            var response = this.httpClient.GetAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            JObject jsonResponse = JObject.Parse(response.Content);

            Assert.True(jsonResponse.HasValues);

            JArray items = (JArray)jsonResponse["Items"];

            Assert.True(items.Count >= STOCKMODELS);
        }

        /// <summary>
        /// Simulation service should return default simulation
        /// </summary>
        [Fact]
        public void Should_Return_Default_Simulation()
        {
            // Arrange
            var request = new HttpRequest(DS_ADDRESS + "/simulations/1");
            request.AddHeader("X-Foo", "Bar");

            // Act
            var response = this.httpClient.GetAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            JObject jsonResponse = JObject.Parse(response.Content);
            JToken jId = jsonResponse["Id"];
            JToken jEtag = jsonResponse["ETag"];
            JToken jDeviceModels = jsonResponse["DeviceModels"];
            JToken jEnabled = jsonResponse["Enabled"];

            string id = (string)jId;
            string etag =(string)jEtag;
            bool enabled = (bool)jEnabled;

            Assert.True(jsonResponse.HasValues);
            Assert.Equal("1", id);
            Assert.False(string.IsNullOrEmpty(etag));
            Assert.True(jDeviceModels.HasValues);
            Assert.False(enabled);
        }

        /// <summary>
        /// Simulation service should able to create a simulation
        /// </summary>
        [Fact]
        public void Should_Create_A_Simulation()
        {
            // Arrage
            var simulation = JObject.Parse(@"{  
                'ETag': 'etag',
                'Enabled': false,
                'Name': 'simulation test',
                'DeviceModels': [  
                    {  
                        'Id': 'model_1',
                        'Count': 150
                    }
                ],
                'IoTHub': {
                    ConnectionString: 'default'
                }
            }");
            var request = new HttpRequest(DS_ADDRESS + "/simulations");
            request.AddHeader("Content-Type", "application/json");
            request.SetContent(simulation);

            // Act
            var response = this.httpClient.PostAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            JObject jsonResponse = JObject.Parse(response.Content);

            Assert.True(jsonResponse.HasValues);
            
            foreach(KeyValuePair<string, JToken> sourceProperty in simulation)
            {
                JProperty targetProp = jsonResponse.Property(sourceProperty.Key);

                if ((string)sourceProperty.Key == "ETag")
                {
                    // The Etag will be changed with success upsert/insert
                    Assert.False(JToken.DeepEquals(sourceProperty.Value, targetProp.Value));
                }
                else
                {
                    Assert.True(JToken.DeepEquals(sourceProperty.Value, targetProp.Value));
                }
            }
        }

        /// <summary>
        /// Simulation service should able to retrieve a simulation by id
        /// </summary>
        [Fact]
        public void Should_Return_A_Simulation_By_Id()
        {
            // Arrage
            var simulation = JObject.Parse(@"{  
                'ETag': 'etag',
                'Enabled': false,
                'Name': 'simulation test',
                'DeviceModels': [  
                    {  
                        'Id': 'model_1',
                        'Count': 150
                    }
                ],
                'IoTHub': {
                    ConnectionString: 'default'
                }
            }");
            IHttpResponse postResponse = this.Create_A_Simulation_In_Storage(simulation);
            JObject postJsonResponse = JObject.Parse(postResponse.Content);
            string id = (string)postJsonResponse["Id"];

            // Act
            var request = new HttpRequest(DS_ADDRESS + $"/simulations/{id}");
            request.AddHeader("X-Foo", "Bar");
            var response = this.httpClient.GetAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            JObject jsonResponse = JObject.Parse(response.Content);

            Assert.True(jsonResponse.HasValues);

            foreach (KeyValuePair<string, JToken> sourceProperty in postJsonResponse)
            {
                JProperty targetProp = jsonResponse.Property(sourceProperty.Key);
                Assert.True(JToken.DeepEquals(sourceProperty.Value, targetProp.Value));
            }
        }

        /// <summary>
        /// Simulation service should able to upsert a simulation by id
        /// </summary>
        [Fact]
        public void Should_Upsert_A_Simulation_By_Id()
        {
            // Arrage
            var simulation = JObject.Parse(@"{  
                'ETag': 'etag',
                'Enabled': false,
                'Name': 'simulation test',
                'DeviceModels': [  
                    {  
                        'Id': 'model_1',
                        'Count': 150
                    }
                ],
                'IoTHub': {
                    ConnectionString: 'default'
                }
            }");
            IHttpResponse postResponse = this.Create_A_Simulation_In_Storage(simulation);
            JObject postJsonResponse = JObject.Parse(postResponse.Content);
            string id = (string)postJsonResponse["Id"];

            // Update simulation
            simulation["ETag"] = (string)postJsonResponse["ETag"];
            simulation["Name"] = "Updated Name";
            simulation["StartTime"] = "NOW";
            simulation["EndTime"] = "NOW+PT2H";
            simulation["DeviceModels"] = @"[  
                {
                    'Id': 'model_2',
                    'Count': 250
                }
            ]";

            // Act
            var request = new HttpRequest(DS_ADDRESS + $"/simulations/{id}");
            request.AddHeader("X-Foo", "Bar");
            var response = this.httpClient.GetAsync(request).Result;
            
            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            JObject jsonResponse = JObject.Parse(response.Content);

            Assert.True(jsonResponse.HasValues);

            foreach (KeyValuePair<string, JToken> sourceProperty in postJsonResponse)
            {
                JProperty targetProp = jsonResponse.Property(sourceProperty.Key);
                Assert.True(JToken.DeepEquals(sourceProperty.Value, targetProp.Value));
            }
        }

        /// <summary>
        /// Simulation service should able to delete a simulation by id
        /// </summary>
        [Fact]
        public void Should_Delete_A_Simulation_By_Id()
        {
            // Arrage
            var simulation = JObject.Parse(@"{  
                'ETag': 'etag',
                'Enabled': false,
                'Name': 'simulation test',
                'DeviceModels': [  
                    {  
                        'Id': 'model_1',
                        'Count': 150
                    }
                ],
                'IoTHub': {
                    ConnectionString: 'default'
                }
            }");
            IHttpResponse postResponse = this.Create_A_Simulation_In_Storage(simulation);
            JObject postJsonResponse = JObject.Parse(postResponse.Content);
            string id = (string)postJsonResponse["Id"];

            // Act
            var request = new HttpRequest(DS_ADDRESS + $"/simulations/{id}");
            request.AddHeader("X-Foo", "Bar");
            var response = this.httpClient.DeleteAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private IHttpResponse Create_A_Simulation_In_Storage(JObject simulation)
        {
            
            var request = new HttpRequest(DS_ADDRESS + "/simulations");
            request.AddHeader("Content-Type", "application/json");
            request.SetContent(simulation);

            return this.httpClient.PostAsync(request).Result;
        }
    }
}
