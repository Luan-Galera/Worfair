namespace Worfair.Modules.Tenants.Domain.Aggregates.Settings;

using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Configurações do tenant (tenant-owned; PK = tenant_id — docs/database/03 §3).
/// Conteúdos jsonb: hiring_workflow, branding, feature_flags.
/// </summary>
public sealed class TenantSettings : ITenantEntity
{
    private TenantSettings()
    {
        // EF Core
    }

    private TenantSettings(TenantId tenantId, string? hiringWorkflow, string? branding, string? featureFlags)
    {
        TenantId = tenantId;
        HiringWorkflow = hiringWorkflow;
        Branding = branding;
        FeatureFlags = featureFlags;
    }

    public TenantId TenantId { get; private set; }

    public string? HiringWorkflow { get; private set; }

    public string? Branding { get; private set; }

    public string? FeatureFlags { get; private set; }

    public static TenantSettings DefaultFor(TenantId tenantId) => new(tenantId, null, null, null);

    public Result UpdateHiringWorkflow(string? hiringWorkflowJson)
    {
        if (!IsValidJsonOrNull(hiringWorkflowJson))
            return Result.Failure(SettingsErrors.InvalidJson);

        HiringWorkflow = hiringWorkflowJson;
        return Result.Success();
    }

    public Result UpdateBranding(string? brandingJson)
    {
        if (!IsValidJsonOrNull(brandingJson))
            return Result.Failure(SettingsErrors.InvalidJson);

        Branding = brandingJson;
        return Result.Success();
    }

    public Result UpdateFeatureFlags(string? featureFlagsJson)
    {
        if (!IsValidJsonOrNull(featureFlagsJson))
            return Result.Failure(SettingsErrors.InvalidJson);

        FeatureFlags = featureFlagsJson;
        return Result.Success();
    }

    void ITenantEntity.SetTenantId(TenantId tenantId)
        => TenantId = tenantId;

    private static bool IsValidJsonOrNull(string? value)
    {
        if (value is null)
            return true;

        try
        {
            _ = System.Text.Json.JsonDocument.Parse(value);
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
