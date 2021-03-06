using System;
using System.Threading;
using AuthorizeService;
using AuthorizeService.Entities;
using AuthorizeService.Factories;
using AuthorizeService.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.EventSourcing;
using Payment.EventSourcing.Messages;
using Xunit;

namespace AuthoriseService.UnitTests.Application
{
    public class AuthoriseApplicationServiceTests : IClassFixture<AuthorisationFixture>
    {
        private readonly AuthorisationFixture _fixture;

        public AuthoriseApplicationServiceTests(AuthorisationFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void Should_authorise_request_when_command_is_received()
        {
            var logger = new Mock<ILogger<AuthorizeService.Services.AuthoriseService>>();
            var mediator = new Mock<IMediator>();
            var authorisationService = new Mock<AuthorisationFactory>();
            var cardService = new Mock<CanValidateCreditCard>();
            var merchantService = new Mock<CanValidateMerchant>();

            Guid transactionId = Guid.NewGuid();
            cardService.Setup(c => c.IsCreditCardValid(It.IsAny<CreditCard>(), It.IsAny<DateTime>())).Returns(true);
            merchantService.Setup(m => m.IsMerchantValidAsync(It.IsAny<Guid>())).ReturnsAsync(true);
            authorisationService.Setup(a => a.CreateAuthorisation(It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<CreditCard>(),
                It.IsAny<Currency>(), It.IsAny<decimal>())).Returns(new Authorisation(transactionId));

            var authoriseAppService = new AuthorizeService.Services.AuthoriseService(logger.Object, mediator.Object,
                authorisationService.Object, cardService.Object, merchantService.Object);
            authoriseAppService.AuthoriseAsync(_fixture.Command);

            authorisationService.Verify(
                a => a.CreateAuthorisation(_fixture.Command.TransactionId, _fixture.Command.MerchantId,
                    _fixture.CreditCard, _fixture.Command.Currency, _fixture.Command.Amount), Times.Once);
            mediator.Verify(m => m.Publish(It.IsAny<AuthorisationCreated>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void Should_decline_request_when_merchant_is_invalid()
        {
            var logger = new Mock<ILogger<AuthorizeService.Services.AuthoriseService>>();
            var mediator = new Mock<IMediator>();
            var authorisationService = new Mock<AuthorisationFactory>();
            var cardService = new Mock<CanValidateCreditCard>();
            var merchantService = new Mock<CanValidateMerchant>();

            cardService.Setup(c => c.IsCreditCardValid(It.IsAny<CreditCard>(), It.IsAny<DateTime>())).Returns(true);
            merchantService.Setup(m => m.IsMerchantValidAsync(It.IsAny<Guid>())).ReturnsAsync(false);

            var authoriseAppService = new AuthorizeService.Services.AuthoriseService(logger.Object, mediator.Object,
                authorisationService.Object, cardService.Object, merchantService.Object);
            authoriseAppService.AuthoriseAsync(_fixture.Command);

            authorisationService.Verify(
                a => a.CreateAuthorisation(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreditCard>(),
                    It.IsAny<Currency>(), It.IsAny<decimal>()), Times.Never);
            mediator.Verify(m => m.Publish(It.IsAny<AuthorisationRejected>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void Should_decline_request_when_card_is_invalid()
        {
            var logger = new Mock<ILogger<AuthorizeService.Services.AuthoriseService>>();
            var mediator = new Mock<IMediator>();
            var authorisationService = new Mock<AuthorisationFactory>();
            var cardService = new Mock<CanValidateCreditCard>();
            var merchantService = new Mock<CanValidateMerchant>();

            cardService.Setup(c => c.IsCreditCardValid(It.IsAny<CreditCard>(), It.IsAny<DateTime>())).Returns(false);
            merchantService.Setup(m => m.IsMerchantValidAsync(It.IsAny<Guid>())).ReturnsAsync(true);

            var authoriseAppService = new AuthorizeService.Services.AuthoriseService(logger.Object, mediator.Object,
                authorisationService.Object, cardService.Object, merchantService.Object);
            authoriseAppService.AuthoriseAsync(_fixture.Command);

            authorisationService.Verify(
                a => a.CreateAuthorisation(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreditCard>(),
                    It.IsAny<Currency>(), It.IsAny<decimal>()), Times.Never);
            mediator.Verify(m => m.Publish(It.IsAny<AuthorisationRejected>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void Should_decline_request_when_both_card_and_merchant_are_invalid()
        {
            var logger = new Mock<ILogger<AuthorizeService.Services.AuthoriseService>>();
            var mediator = new Mock<IMediator>();
            var authorisationService = new Mock<AuthorisationFactory>();
            var cardService = new Mock<CanValidateCreditCard>();
            var merchantService = new Mock<CanValidateMerchant>();

            cardService.Setup(c => c.IsCreditCardValid(It.IsAny<CreditCard>(), It.IsAny<DateTime>())).Returns(false);
            merchantService.Setup(m => m.IsMerchantValidAsync(It.IsAny<Guid>())).ReturnsAsync(false);

            var authoriseAppService = new AuthorizeService.Services.AuthoriseService(logger.Object, mediator.Object,
                authorisationService.Object, cardService.Object, merchantService.Object);
            authoriseAppService.AuthoriseAsync(_fixture.Command);

            authorisationService.Verify(
                a => a.CreateAuthorisation(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreditCard>(),
                    It.IsAny<Currency>(), It.IsAny<decimal>()), Times.Never);
            mediator.Verify(m => m.Publish(It.IsAny<AuthorisationRejected>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void Should_log_error_when_exception_thrown_while_trying_to_create_authorisation()
        {
            var logger = new Mock<ILogger<AuthorizeService.Services.AuthoriseService>>();
            var mediator = new Mock<IMediator>();
            var authorisationService = new Mock<AuthorisationFactory>();
            var cardService = new Mock<CanValidateCreditCard>();
            var merchantService = new Mock<CanValidateMerchant>();

            cardService.Setup(c => c.IsCreditCardValid(It.IsAny<CreditCard>(), It.IsAny<DateTime>())).Returns(true);
            merchantService.Setup(m => m.IsMerchantValidAsync(It.IsAny<Guid>())).ReturnsAsync(true);
            authorisationService.Setup(a => a.CreateAuthorisation(It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<CreditCard>(),
                It.IsAny<Currency>(), It.IsAny<decimal>())).Throws(new Exception());

            var authoriseAppService = new AuthorizeService.Services.AuthoriseService(logger.Object, mediator.Object,
                authorisationService.Object, cardService.Object, merchantService.Object);
            authoriseAppService.AuthoriseAsync(_fixture.Command);

            mediator.Verify(m => m.Publish(It.IsAny<AuthorisationRejected>(), It.IsAny<CancellationToken>()),
                Times.Never);
            mediator.Verify(m => m.Publish(It.IsAny<AuthorisationCreated>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public void Should_reject_transaction_with_amount_zero()
        {
            var logger = new Mock<ILogger<AuthorizeService.Services.AuthoriseService>>();
            var mediator = new Mock<IMediator>();
            var authorisationService = new Mock<AuthorisationFactory>();
            var cardService = new Mock<CanValidateCreditCard>();
            var merchantService = new Mock<CanValidateMerchant>();

            Guid transactionId = Guid.NewGuid();
            cardService.Setup(c => c.IsCreditCardValid(It.IsAny<CreditCard>(), It.IsAny<DateTime>())).Returns(true);
            merchantService.Setup(m => m.IsMerchantValidAsync(It.IsAny<Guid>())).ReturnsAsync(true);
            authorisationService.Setup(a => a.CreateAuthorisation(It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<CreditCard>(),
                It.IsAny<Currency>(), It.IsAny<decimal>())).Returns(new Authorisation(transactionId));

            var authoriseAppService = new AuthorizeService.Services.AuthoriseService(logger.Object, mediator.Object,
                authorisationService.Object, cardService.Object, merchantService.Object);

            Assert.ThrowsAsync<InvalidOperationException>(() => authoriseAppService.AuthoriseAsync(_fixture.InvalidCommand));
        }
    }
}