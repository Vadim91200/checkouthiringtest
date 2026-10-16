using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly PaymentsRepository _repository;
    private readonly IAcquiringBank _bank;

    public PaymentsController(PaymentsRepository repository, IAcquiringBank bank)
    {
        _repository = repository;
        _bank = bank;
    }

    /// <summary>Validate, ask the bank, store, answer. Invalid input is a 400 Rejected (see Program.cs).</summary>
    [HttpPost]
    [ProducesResponseType<Payment>(StatusCodes.Status201Created)]
    [ProducesResponseType<RejectedResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<Payment>> ProcessPaymentAsync(PostPaymentRequest request, CancellationToken cancellationToken)
    {
        bool authorized;
        try
        {
            authorized = await _bank.AuthorizeAsync(request, cancellationToken);
        }
        catch (BankUnavailableException exception)
        {
            // The request was valid but the bank gave no decision, so there is
            // no payment to store or report: the merchant may retry.
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "acquiring bank unavailable", detail = exception.Message });
        }

        // Declined is a payment too (the bank made a decision), so it is kept
        // and retrievable for reconciliation, exactly like an authorized one.
        Payment payment = Payment.From(request, authorized);
        _repository.Add(payment);
        return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, payment);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<Payment>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Payment> GetPayment(Guid id)
    {
        return _repository.Get(id) is { } payment ? payment : NotFound(new { error = "payment not found" });
    }
}