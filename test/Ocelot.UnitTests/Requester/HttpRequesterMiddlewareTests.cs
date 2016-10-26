﻿using System;
using System.IO;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Ocelot.Infrastructure.RequestData;
using Ocelot.RequestBuilder;
using Ocelot.Requester;
using Ocelot.Requester.Middleware;
using Ocelot.Responder;
using Ocelot.Responses;
using TestStack.BDDfy;
using Xunit;

namespace Ocelot.UnitTests.Requester
{
    public class HttpRequesterMiddlewareTests : IDisposable
    {
        private readonly Mock<IHttpRequester> _requester;
        private readonly Mock<IRequestScopedDataRepository> _scopedRepository;
        private readonly string _url;
        private readonly TestServer _server;
        private readonly HttpClient _client;
        private HttpResponseMessage _result;
        private OkResponse<HttpResponseMessage> _response;
        private OkResponse<Request> _request;
        private readonly Mock<IHttpResponder> _responder;

        public HttpRequesterMiddlewareTests()
        {
            _url = "http://localhost:51879";
            _requester = new Mock<IHttpRequester>();
            _scopedRepository = new Mock<IRequestScopedDataRepository>();
            _responder = new Mock<IHttpResponder>();

            var builder = new WebHostBuilder()
              .ConfigureServices(x =>
              {
                  x.AddSingleton(_responder.Object);
                  x.AddSingleton(_requester.Object);
                  x.AddSingleton(_scopedRepository.Object);
              })
              .UseUrls(_url)
              .UseKestrel()
              .UseContentRoot(Directory.GetCurrentDirectory())
              .UseIISIntegration()
              .UseUrls(_url)
              .Configure(app =>
              {
                  app.UseHttpRequesterMiddleware();
              });

            _server = new TestServer(builder);
            _client = _server.CreateClient();
        }

        [Fact]
        public void happy_path()
        {
            this.Given(x => x.GivenTheRequestIs(new Request(new HttpRequestMessage(),new CookieContainer())))
                .And(x => x.GivenTheRequesterReturns(new HttpResponseMessage()))
                .And(x => x.GivenTheResponderReturns())
                .When(x => x.WhenICallTheMiddleware())
                .Then(x => x.ThenTheResponderIsCalledCorrectly())
                .BDDfy();
        }

        private void GivenTheRequesterReturns(HttpResponseMessage response)
        {
            _response = new OkResponse<HttpResponseMessage>(response);
            _requester
                .Setup(x => x.GetResponse(It.IsAny<Request>()))
                .ReturnsAsync(_response);
        }

        private void GivenTheResponderReturns()
        {
            _responder
                .Setup(x => x.SetResponseOnHttpContext(It.IsAny<HttpContext>(), _response.Data))
                .ReturnsAsync(new OkResponse());
        }

        private void ThenTheResponderIsCalledCorrectly()
        {
            _responder
                .Verify(x => x.SetResponseOnHttpContext(It.IsAny<HttpContext>(), _response.Data), Times.Once());
        }

        private void WhenICallTheMiddleware()
        {
            _result = _client.GetAsync(_url).Result;
        }

        private void GivenTheRequestIs(Request request)
        {
            _request = new OkResponse<Request>(request);
            _scopedRepository
                .Setup(x => x.Get<Request>(It.IsAny<string>()))
                .Returns(_request);
        }

        public void Dispose()
        {
            _client.Dispose();
            _server.Dispose();
        }
    }
}