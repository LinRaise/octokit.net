﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NSubstitute;
using Octokit.Tests.Helpers;
using Xunit;

namespace Octokit.Tests.Clients
{
    /// <summary>
    /// Client tests mostly just need to make sure they call the IApiConnection with the correct 
    /// relative Uri. No need to fake up the response. All *those* tests are in ApiConnectionTests.cs.
    /// </summary>
    public class AuthorizationsClientTests
    {
        public class TheConstructor
        {
            [Fact]
            public void ThrowsForBadArgs()
            {
                Assert.Throws<ArgumentNullException>(() => new AuthorizationsClient(null));
            }
        }

        public class TheGetAllMethod
        {
            [Fact]
            public void GetsAListOfAuthorizations()
            {
                var client = Substitute.For<IApiConnection<Authorization>>();
                var authEndpoint = new AuthorizationsClient(client);

                authEndpoint.GetAll();

                client.Received().GetAll(Arg.Is<Uri>(u => u.ToString() == "/authorizations"), null);
            }
        }

        public class TheGetMethod
        {
            [Fact]
            public void GetsAnAuthorization()
            {
                var client = Substitute.For<IApiConnection<Authorization>>();
                var authEndpoint = new AuthorizationsClient(client);

                authEndpoint.Get(1);

                client.Received().Get(Arg.Is<Uri>(u => u.ToString() == "/authorizations/1"), null);
            }
        }

        public class TheUpdateAsyncMethod
        {
            [Fact]
            public void SendsUpdateToCorrectUrl()
            {
                var client = Substitute.For<IApiConnection<Authorization>>();
                var authEndpoint = new AuthorizationsClient(client);

                authEndpoint.Update(1, new AuthorizationUpdate());

                client.Received().Update(Arg.Is<Uri>(u => u.ToString() == "/authorizations/1"),
                    Args.AuthorizationUpdate);
            }
        }

        public class TheCreateAsyncMethod
        {
            [Fact]
            public void SendsCreateToCorrectUrl()
            {
                var client = Substitute.For<IApiConnection<Authorization>>();
                var authEndpoint = new AuthorizationsClient(client);

                authEndpoint.Create(new AuthorizationUpdate());

                client.Received().Create(Arg.Is<Uri>(u => u.ToString() == "/authorizations")
                    , Args.AuthorizationUpdate);
            }
        }

        public class TheDeleteAsyncMethod
        {
            [Fact]
            public void DeletesCorrectUrl()
            {
                var client = Substitute.For<IApiConnection<Authorization>>();
                var authEndpoint = new AuthorizationsClient(client);

                authEndpoint.Delete(1);

                client.Received().Delete(Arg.Is<Uri>(u => u.ToString() == "/authorizations/1"));
            }
        }

        public class TheGetOrCreateApplicationAuthenticationMethod
        {
            [Fact]
            public void GetsOrCreatesAuthenticationAtCorrectUrl()
            {
                var data = new AuthorizationUpdate();
                var client = Substitute.For<IApiConnection<Authorization>>();
                var authEndpoint = new AuthorizationsClient(client);

                authEndpoint.GetOrCreateApplicationAuthentication("clientId", "secret", data);

                client.Received().GetOrCreate(Arg.Is<Uri>(u => u.ToString() == "/authorizations/clients/clientId"),
                    Args.Object);
            }

            [Fact]
            public async Task WrapsTwoFactorFailureWithTwoFactorException()
            {
                var data = new AuthorizationUpdate();
                var client = Substitute.For<IApiConnection<Authorization>>();
                client.GetOrCreate(Args.Uri, Args.Object, Args.String).Returns(_ => {throw new AuthorizationException();});
                var authEndpoint = new AuthorizationsClient(client);

                AssertEx.Throws<TwoFactorChallengeFailedException>(async () =>
                    await authEndpoint.GetOrCreateApplicationAuthentication("clientId", "secret", data));
            }

            [Fact]
            public async Task UsesCallbackToRetrieveTwoFactorCode()
            {
                var twoFactorChallengeResult = new TwoFactorChallengeResult("two-factor-code");
                var data = new AuthorizationUpdate { Note = "note" };
                var client = Substitute.For<IAuthorizationsClient>();
                client.GetOrCreateApplicationAuthentication("clientId", "secret", Arg.Any<AuthorizationUpdate>())
                    .Returns(_ => {throw new TwoFactorRequiredException();});
                client.GetOrCreateApplicationAuthentication("clientId",
                    "secret",
                    Arg.Any<AuthorizationUpdate>(),
                    "two-factor-code")
                    .Returns(Task.Factory.StartNew(() => new Authorization {Token = "xyz"}));

                var result = await client.GetOrCreateApplicationAuthentication("clientId",
                    "secret",
                    data,
                    e => Task.Factory.StartNew(() => twoFactorChallengeResult));

                client.Received().GetOrCreateApplicationAuthentication("clientId",
                    "secret",
                    Arg.Is<AuthorizationUpdate>(u => u.Note == "note"));
                client.Received().GetOrCreateApplicationAuthentication("clientId",
                    "secret",
                    Arg.Any<AuthorizationUpdate>(), "two-factor-code");
                Assert.Equal("xyz", result.Token);
            }

            [Fact]
            public async Task RetriesWhenResendRequested()
            {
                var challengeResults = new Queue<TwoFactorChallengeResult>(new []
                {
                    TwoFactorChallengeResult.RequestResendCode,
                    new TwoFactorChallengeResult("two-factor-code")
                });
                var data = new AuthorizationUpdate();
                var client = Substitute.For<IAuthorizationsClient>();
                client.GetOrCreateApplicationAuthentication("clientId", "secret", Arg.Any<AuthorizationUpdate>())
                    .Returns(_ => { throw new TwoFactorRequiredException(); });
                client.GetOrCreateApplicationAuthentication("clientId",
                    "secret",
                    Arg.Any<AuthorizationUpdate>(),
                    "two-factor-code")
                    .Returns(Task.Factory.StartNew(() => new Authorization { Token = "xyz" }));

                var result = await client.GetOrCreateApplicationAuthentication("clientId",
                    "secret",
                    data,
                    e => Task.Factory.StartNew(() => challengeResults.Dequeue()));

                client.Received().GetOrCreateApplicationAuthentication("clientId",
                    "secret",
                    Arg.Any<AuthorizationUpdate>());
                client.Received().GetOrCreateApplicationAuthentication("clientId",
                    "secret",
                    Arg.Any<AuthorizationUpdate>(), "two-factor-code");
                Assert.Equal("xyz", result.Token);
            }
        }
    }
}
