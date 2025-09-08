using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using XtrXmlTranslator.Core.Translate;
using Xunit;

namespace XtrXmlTranslator.Tests
{
    public class GeminiApiClientSmokeTests
    {
        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
            public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_responder(request));
        }

        [Fact]
        public async Task StreamTranslateAsync_SSE_Yields_Deltas()
        {
            // Arrange: SSE content with two data lines
            var sseLines = string.Join("\n", new[]
            {
                "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Hello \"}]}}]}",
                "",
                "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"World\"}]}}]}",
                "",
                "data: [DONE]",
                ""
            });
            var content = new StreamContent(new System.IO.MemoryStream(Encoding.UTF8.GetBytes(sseLines)));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");

            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            });

            var opt = new GeminiOptions { ApiKey = "dummy", HttpTimeout = TimeSpan.FromSeconds(5) };
            var client = new GeminiApiClient(opt, new HttpClient(handler));

            // Act
            var sb = new StringBuilder();
            await foreach (var delta in client.StreamTranslateAsync("test"))
                sb.Append(delta);

            // Assert
            sb.ToString().Should().Be("Hello World");
        }

        [Fact]
        public async Task StreamTranslateAsync_JSON_Fallback_Yields_Once()
        {
            // Arrange: JSON response with combined text
            var json = "{\n  \"candidates\": [ { \n    \"content\": { \n      \"parts\": [ { \"text\": \"Hello \" }, { \"text\": \"JSON\" } ] \n    } \n  } ]\n}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            });

            var opt = new GeminiOptions { ApiKey = "dummy", HttpTimeout = TimeSpan.FromSeconds(5) };
            var client = new GeminiApiClient(opt, new HttpClient(handler));

            // Act
            var outputs = new List<string>();
            await foreach (var delta in client.StreamTranslateAsync("test"))
                outputs.Add(delta);

            // Assert: JSON fallback emits a single combined text
            outputs.Count.Should().Be(1);
            outputs[0].Should().Be("Hello JSON");
        }
    }
}

