using System.ComponentModel.DataAnnotations;

namespace Safe.Host.Contracts;

public sealed record ReverseRequest(
    [Required, StringLength(512, MinimumLength = 1)] string Comment);
