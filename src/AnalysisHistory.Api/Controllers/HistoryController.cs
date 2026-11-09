using AnalysisHistory.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AnalysisHistory.Api.Controllers;

/// <summary>
/// Read-only API for browsing stored analyses and downloading text reports.
/// </summary>
[ApiController]
[Route("api/history")]
public class HistoryController : ControllerBase
{
    private readonly AnalysisDbContext _db;

    /// <summary>
    /// Injects the SQLite-backed EF Core context.
    /// </summary>
    public HistoryController(AnalysisDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns a lightweight list of all analyses, newest first.
    /// </summary>
    [HttpGet("all")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        // SQLite cannot translate ORDER BY on DateTimeOffset, so project first,
        // then sort in memory (the result set is small for this lab).
        var items = await _db.Analyses
            .AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.FileName,
                x.WordCount,
                x.UniqueWordCount,
                x.CreatedAt
            })
            .ToListAsync(ct);

        // Newest analyses first; done client-side because of the SQLite limitation.
        return Ok(items.OrderByDescending(x => x.CreatedAt));
    }

    /// <summary>
    /// Returns the full stored analysis, including extracted text and keywords.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await _db.Analyses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>
    /// Generates a plain-text report for one analysis and returns it as a file download.
    /// </summary>
    [HttpGet("{id:guid}/report")]
    public async Task<IActionResult> GetReport(Guid id, CancellationToken ct)
    {
        var item = await _db.Analyses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (item is null)
            return NotFound();

        var report =
            $"ATASKAITA\n" +
            $"Failas: {item.FileName}\n" +
            $"Data: {item.CreatedAt:yyyy-MM-dd HH:mm}\n" +
            $"Žodžių skaičius: {item.WordCount}\n" +
            $"Unikalių žodžių: {item.UniqueWordCount}\n" +
            $"Raktažodžiai: {item.TopKeywordsJson}\n\n" +
            $"TEKSTAS:\n{item.Text}\n";

        var bytes = System.Text.Encoding.UTF8.GetBytes(report);

        // Return a downloadable .txt file with UTF-8 encoding.
        return File(
            bytes,
            "text/plain; charset=utf-8",
            $"report-{item.Id}.txt");
    }
}
