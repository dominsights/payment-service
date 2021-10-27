using System;
using System.Threading.Tasks;
using AuthorizeService.Factories;
using AuthorizeService.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AuthorizeService.Application
{
    public class AuthoriseApplicationService
    {
        private readonly ILogger<AuthoriseApplicationService> _logger;
        private readonly IMediator _mediator;
        private readonly AuthorisationFactory _authorisationFactory;
        private readonly CanValidateCreditCard _cardService;
        private readonly CanValidateMerchant _canValidateMerchant;

        public AuthoriseApplicationService(ILogger<AuthoriseApplicationService> logger, IMediator mediator, 
            AuthorisationFactory authorisationFactory, CanValidateCreditCard cardService, CanValidateMerchant canValidateMerchant)
        {
            _logger = logger;
            _mediator = mediator;
            _authorisationFactory = authorisationFactory;
            _cardService = cardService;
            _canValidateMerchant = canValidateMerchant;
        }
        
        public async Task AuthoriseAsync(AuthorisationCommand authoriseCommand)
        {
            try
            {
                if (_cardService.IsCreditCardValid(authoriseCommand.CreditCard) &&
                    await _canValidateMerchant.IsMerchantValidAsync(authoriseCommand.MerchantId))
                {
                    var authorisation = _authorisationFactory.CreateAuthorisation(authoriseCommand.MerchantId,
                        authoriseCommand.CreditCard,
                        authoriseCommand.Currency, authoriseCommand.Amount);
                    await _mediator.Send(new AuthorisationCreated(authoriseCommand.MerchantId, authorisation.Id));
                    _logger.LogInformation($"[Authorise] Authorisation created with id: {authorisation.Id}");
                    return;
                }

                _logger.LogWarning($"[Authorise] Authorisation rejected for merchant with id: {authoriseCommand.MerchantId}");
                await _mediator.Send(new AuthorisationRejected(authoriseCommand.MerchantId, authoriseCommand.CreditCard.Number));
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"[Authorise] Error when trying to authorise command for merchant with id: {authoriseCommand.MerchantId}");
            }
        }
    }
}