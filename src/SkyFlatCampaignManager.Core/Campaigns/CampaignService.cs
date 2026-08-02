using SkyFlatCampaignManager.Core.Utilities;

namespace SkyFlatCampaignManager.Core.Campaigns;

public sealed class CampaignService : ICampaignService
{
    private readonly ICampaignRepository _repository;
    private readonly IClock _clock;

    public CampaignService(ICampaignRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<CampaignState> GetOrCreateAsync(
        string campaignKey,
        string profileId,
        IEnumerable<FilterCampaignSettings> filters,
        CampaignOptions options,
        CancellationToken ct = default)
    {
        var existing = await _repository.LoadAsync(campaignKey, ct).ConfigureAwait(false);
        var requirement = Evaluate(existing, options);
        if (existing is not null && !requirement.IsRequired)
        {
            return existing;
        }

        if (existing is not null && requirement.IsIncomplete && !requirement.IsExpired && !requirement.IsInvalidated)
        {
            SyncFilterTargets(existing, filters);
            await _repository.SaveAsync(campaignKey, existing, ct).ConfigureAwait(false);
            return existing;
        }

        if (existing is not null && requirement.IsExpired && !options.AutoStartExpiredCampaign)
        {
            existing.Status = CampaignStatus.Expired;
            await _repository.SaveAsync(campaignKey, existing, ct).ConfigureAwait(false);
            return existing;
        }

        var created = CreateNew(campaignKey, profileId, filters, options);
        await _repository.SaveAsync(campaignKey, created, ct).ConfigureAwait(false);
        return created;
    }

    public async Task<CampaignRequirement> EvaluateRequirementAsync(string campaignKey, CampaignOptions options, CancellationToken ct = default)
    {
        var state = await _repository.LoadAsync(campaignKey, ct).ConfigureAwait(false);
        return Evaluate(state, options);
    }

    public async Task<CampaignState> AcceptFlatAsync(
        string campaignKey,
        string filterName,
        double exposureSeconds,
        double measuredAdu,
        double? measuredHistogramFraction = null,
        double? sunAltitudeDegrees = null,
        CancellationToken ct = default)
    {
        var state = await RequireAsync(campaignKey, ct).ConfigureAwait(false);
        if (!state.Filters.TryGetValue(filterName, out var filter))
        {
            filter = new FilterProgress { FilterName = filterName, Target = 0 };
            state.Filters[filterName] = filter;
        }

        if (filter.IsComplete)
        {
            return state;
        }

        filter.Accepted++;
        filter.LastExposureSeconds = exposureSeconds;
        filter.LastMeasuredAdu = measuredAdu;
        if (measuredHistogramFraction is { } fraction)
        {
            filter.LastMeasuredHistogramFraction = fraction;
        }

        if (sunAltitudeDegrees is { } altitude)
        {
            // Persisted here (not by the caller mutating the returned object afterwards) so this
            // survives a plugin/NINA restart — the whole point of ClosestToOptimalWindow learning.
            filter.LastSunAltitudeDegrees = altitude;
        }

        MaybeComplete(state);
        await _repository.SaveAsync(campaignKey, state, ct).ConfigureAwait(false);
        return state;
    }

    public async Task RejectFlatAsync(string campaignKey, string filterName, string reason, CancellationToken ct = default)
    {
        var state = await RequireAsync(campaignKey, ct).ConfigureAwait(false);
        if (state.Filters.TryGetValue(filterName, out var filter))
        {
            filter.Rejected++;
            await _repository.SaveAsync(campaignKey, state, ct).ConfigureAwait(false);
        }

        _ = reason;
    }

    public async Task<CampaignState> MarkCompletedAsync(string campaignKey, CampaignOptions options, CancellationToken ct = default)
    {
        var state = await RequireAsync(campaignKey, ct).ConfigureAwait(false);
        state.Status = CampaignStatus.Completed;
        state.CompletedAtUtc = _clock.UtcNow;
        state.ValidUntilUtc = _clock.UtcNow.AddDays(Math.Max(1, options.ValidityDays));
        await _repository.SaveAsync(campaignKey, state, ct).ConfigureAwait(false);
        return state;
    }

    public async Task<CampaignState> InvalidateAsync(string campaignKey, string reason, CancellationToken ct = default)
    {
        var state = await _repository.LoadAsync(campaignKey, ct).ConfigureAwait(false)
                    ?? new CampaignState { CampaignId = campaignKey, CreatedAtUtc = _clock.UtcNow };
        state.Status = CampaignStatus.Invalidated;
        state.InvalidatedAtUtc = _clock.UtcNow;
        state.InvalidationReason = reason;
        state.ValidUntilUtc = null;
        await _repository.SaveAsync(campaignKey, state, ct).ConfigureAwait(false);
        return state;
    }

    public async Task<CampaignState> ResetFilterAsync(string campaignKey, string filterName, CancellationToken ct = default)
    {
        var state = await RequireAsync(campaignKey, ct).ConfigureAwait(false);
        if (state.Filters.TryGetValue(filterName, out var filter))
        {
            filter.Accepted = 0;
            filter.Rejected = 0;
            state.Status = CampaignStatus.InProgress;
            state.CompletedAtUtc = null;
            state.ValidUntilUtc = null;
            await _repository.SaveAsync(campaignKey, state, ct).ConfigureAwait(false);
        }

        return state;
    }

    public async Task<CampaignState> ResetAllAsync(
        string campaignKey,
        IEnumerable<FilterCampaignSettings> filters,
        string profileId,
        CampaignOptions options,
        CancellationToken ct = default)
    {
        var created = CreateNew(campaignKey, profileId, filters, options);
        await _repository.SaveAsync(campaignKey, created, ct).ConfigureAwait(false);
        return created;
    }

    public async Task ExtendValidityAsync(string campaignKey, DateTime validUntilUtc, CancellationToken ct = default)
    {
        var state = await RequireAsync(campaignKey, ct).ConfigureAwait(false);
        state.ValidUntilUtc = DateTime.SpecifyKind(validUntilUtc, DateTimeKind.Utc);
        if (state.Status == CampaignStatus.Expired)
        {
            state.Status = CampaignStatus.Completed;
        }

        await _repository.SaveAsync(campaignKey, state, ct).ConfigureAwait(false);
    }

    private CampaignRequirement Evaluate(CampaignState? state, CampaignOptions options)
    {
        if (state is null || state.Status == CampaignStatus.None)
        {
            return new CampaignRequirement
            {
                IsRequired = true,
                NoCampaign = true,
                Reason = "No campaign exists."
            };
        }

        if (state.Status == CampaignStatus.Invalidated)
        {
            return new CampaignRequirement
            {
                IsRequired = true,
                IsInvalidated = true,
                Reason = state.InvalidationReason ?? "Campaign invalidated.",
                Campaign = state
            };
        }

        var expired = state.Status == CampaignStatus.Expired
                      || (state.ValidUntilUtc is { } until && until <= _clock.UtcNow)
                      || (state.Status == CampaignStatus.Completed
                          && state.CompletedAtUtc is { } completed
                          && completed.AddDays(Math.Max(1, options.ValidityDays)) <= _clock.UtcNow);

        if (expired)
        {
            return new CampaignRequirement
            {
                IsRequired = true,
                IsExpired = true,
                IsCompleted = state.Status == CampaignStatus.Completed || state.IsComplete,
                Reason = "Campaign expired.",
                Campaign = state
            };
        }

        if (state.Status == CampaignStatus.Completed && state.IsComplete)
        {
            return new CampaignRequirement
            {
                IsRequired = false,
                IsCompleted = true,
                Reason = "Campaign completed and still valid.",
                Campaign = state
            };
        }

        if (state.IsIncomplete || state.Status == CampaignStatus.InProgress)
        {
            return new CampaignRequirement
            {
                IsRequired = true,
                IsIncomplete = true,
                Reason = "Campaign incomplete.",
                Campaign = state
            };
        }

        return new CampaignRequirement
        {
            IsRequired = false,
            IsCompleted = true,
            Reason = "Campaign not required.",
            Campaign = state
        };
    }

    private CampaignState CreateNew(string campaignKey, string profileId, IEnumerable<FilterCampaignSettings> filters, CampaignOptions options)
    {
        var state = new CampaignState
        {
            SchemaVersion = PluginIdentity.CurrentSchemaVersion,
            CampaignId = $"{_clock.UtcNow:yyyy-MM-dd}-{campaignKey}",
            ProfileId = profileId,
            CampaignName = options.CampaignName,
            CreatedAtUtc = _clock.UtcNow,
            Status = CampaignStatus.InProgress
        };

        foreach (var filter in filters.Where(f => f.Enabled && f.TargetCount > 0))
        {
            state.Filters[filter.FilterName] = new FilterProgress
            {
                FilterName = filter.FilterName,
                Target = filter.TargetCount,
                MinimumAcceptableCount = filter.MinimumAcceptableCount
            };
        }

        return state;
    }

    private static void SyncFilterTargets(CampaignState state, IEnumerable<FilterCampaignSettings> filters)
    {
        foreach (var filter in filters.Where(f => f.Enabled && f.TargetCount > 0))
        {
            if (!state.Filters.TryGetValue(filter.FilterName, out var progress))
            {
                state.Filters[filter.FilterName] = new FilterProgress
                {
                    FilterName = filter.FilterName,
                    Target = filter.TargetCount,
                    MinimumAcceptableCount = filter.MinimumAcceptableCount
                };
            }
            else
            {
                progress.Target = filter.TargetCount;
                progress.MinimumAcceptableCount = filter.MinimumAcceptableCount;
            }
        }
    }

    private void MaybeComplete(CampaignState state)
    {
        if (state.Filters.Count > 0 && state.Filters.Values.Where(f => f.Target > 0).All(f => f.IsComplete))
        {
            state.Status = CampaignStatus.Completed;
            state.CompletedAtUtc = _clock.UtcNow;
        }
        else
        {
            state.Status = CampaignStatus.InProgress;
        }
    }

    private async Task<CampaignState> RequireAsync(string campaignKey, CancellationToken ct)
    {
        return await _repository.LoadAsync(campaignKey, ct).ConfigureAwait(false)
               ?? throw new InvalidOperationException($"Campaign '{campaignKey}' not found.");
    }
}
