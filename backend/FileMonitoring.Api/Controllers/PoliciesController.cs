using FileMonitoring.Api.Data;
using FileMonitoring.Api.Domain;
using FileMonitoring.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace FileMonitoring.Api.Controllers;

public record PolicyRuleDto(string Field, string Operator, string Value);
public record PolicyDto(string Name, bool Enabled, string Action, List<PolicyRuleDto> Rules);

[ApiController, Route("api/v1/policies"), Authorize(Roles = "Admin")]
public class PoliciesController(AppDbContext db, AuditLogger audit) : ControllerBase
{
    private static readonly string[] AllowedFields = ["application", "extension", "size", "confidence", "process"];
    private static readonly string[] AllowedOps = ["eq", "neq", "gt", "gte", "lt", "lte", "contains"];

    [HttpGet]
    public async Task<IActionResult> List() => Ok(await db.Policies.Include(p => p.Rules).AsNoTracking().ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(PolicyDto dto)
    {
        if (dto.Rules.Count == 0) return BadRequest(new { error = "At least one rule is required" });
        foreach (var r in dto.Rules)
        {
            if (!AllowedFields.Contains(r.Field.ToLowerInvariant())) return BadRequest(new { error = $"Unknown field '{r.Field}'" });
            if (!AllowedOps.Contains(r.Operator.ToLowerInvariant())) return BadRequest(new { error = $"Unknown operator '{r.Operator}'" });
        }
        var p = new Policy { Id = Guid.NewGuid(), Name = dto.Name, Enabled = dto.Enabled, Action = dto.Action,
            Rules = dto.Rules.Select(r => new PolicyRule { Id = Guid.NewGuid(), Field = r.Field, Operator = r.Operator, Value = r.Value }).ToList() };
        db.Policies.Add(p); await db.SaveChangesAsync();
        await audit.LogAsync(User.Identity?.Name ?? "?", "policy.create", "policy", p.Id.ToString(), dto);
        return Ok(p);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, PolicyDto dto)
    {
        var p = await db.Policies.Include(x => x.Rules).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        p.Name = dto.Name; p.Enabled = dto.Enabled; p.Action = dto.Action;
        db.PolicyRules.RemoveRange(p.Rules);
        p.Rules = dto.Rules.Select(r => new PolicyRule { Id = Guid.NewGuid(), PolicyId = id, Field = r.Field, Operator = r.Operator, Value = r.Value }).ToList();
        await db.SaveChangesAsync();
        await audit.LogAsync(User.Identity?.Name ?? "?", "policy.update", "policy", id.ToString(), dto);
        return Ok(p);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var p = await db.Policies.FindAsync(id); if (p is null) return NotFound();
        db.Policies.Remove(p); await db.SaveChangesAsync();
        await audit.LogAsync(User.Identity?.Name ?? "?", "policy.delete", "policy", id.ToString());
        return NoContent();
    }
}
