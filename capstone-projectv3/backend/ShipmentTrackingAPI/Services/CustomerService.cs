using ShipmentTrackingAPI.DTOs.Customer;
using ShipmentTrackingAPI.Interfaces;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Models.Exceptions;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;
using ShipmentTrackingAPI.Services.Interfaces;


namespace ShipmentTrackingAPI.Services;

/// <summary>
/// Handles customer profile management and saved address book.
///
/// Profile data is split across two tables:
///   - users         : email, full_name, password_hash, role (identity)
///   - customer_profiles : phone, alternate_phone, profile_image_url (profile)
/// UpdateProfileAsync only touches customer_profiles — not the users row.
///
/// Saved addresses enforce a single-default rule: at most one address
/// per customer can have is_default = true. Setting a new default
/// clears the previous one in the same transaction.
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepo;

    public CustomerService(ICustomerRepository customerRepo)
    {
        _customerRepo = customerRepo;
    }

    // ─────────────────────────────────────────────────────────
    //  PROFILE MANAGEMENT
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the customer's full profile — identity fields from
    /// the users table plus contact fields from customer_profiles.
    /// The repository joins both tables via the Include on User.
    /// </summary>
    public async Task<CustomerProfileDto> GetProfileAsync(int userId)
    {
        var profile = await _customerRepo.GetByUserIdAsync(userId)
            ?? throw new NotFoundException(
                "Customer profile not found for this account.");

        return MapToProfileDto(profile);
    }

    /// <summary>
    /// Updates contact fields on the customer_profiles row only.
    /// Fields are nullable — a null in the request means "leave unchanged".
    /// full_name and email are identity fields on users and are never
    /// modified through this endpoint.
    /// </summary>
    public async Task<CustomerProfileDto> UpdateProfileAsync(
        int userId,
        UpdateProfileRequestDto req)
    {
        var profile = await _customerRepo.GetByUserIdAsync(userId)
            ?? throw new NotFoundException(
                "Customer profile not found for this account.");

        // Partial update: only overwrite fields that were provided
        if (req.PhoneNumber is not null)
            profile.PhoneNumber = req.PhoneNumber.Trim();

        if (req.AlternatePhoneNumber is not null)
            profile.AlternatePhoneNumber = req.AlternatePhoneNumber.Trim();

        if (req.ProfileImageUrl is not null)
            profile.ProfileImageUrl = req.ProfileImageUrl.Trim();

        profile.UpdatedAt = DateTime.UtcNow;

        await _customerRepo.UpdateAsync(profile);
        await _customerRepo.SaveAsync();

        return MapToProfileDto(profile);
    }

    // ─────────────────────────────────────────────────────────
    //  ADDRESS BOOK MANAGEMENT
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all saved addresses for the customer.
    /// Ordering: default address first, then newest first.
    /// </summary>
    public async Task<List<SavedAddressDto>> GetSavedAddressesAsync(int userId)
    {
        var profile = await _customerRepo.GetByUserIdAsync(userId)
            ?? throw new NotFoundException(
                "Customer profile not found for this account.");

        var addresses = await _customerRepo.GetSavedAddressesAsync(profile.Id);

        return addresses.Select(MapToAddressDto).ToList();
    }

    /// <summary>
    /// Adds a new address to the customer's address book.
    ///
    /// If IsDefault = true: clears the is_default flag on all existing
    /// addresses first so only one default exists at a time.
    /// Both the clear and the insert are flushed in a single SaveAsync.
    /// </summary>
    public async Task<SavedAddressDto> AddSavedAddressAsync(
        int userId,
        SavedAddressRequestDto req)
    {
        var profile = await _customerRepo.GetByUserIdAsync(userId)
            ?? throw new NotFoundException(
                "Customer profile not found for this account.");

        // Enforce single-default rule before inserting
        if (req.IsDefault)
            await _customerRepo.ClearDefaultAddressAsync(profile.Id);

        var address = new SavedAddress
        {
            CustomerId           = profile.Id,
            Label                = req.Label.Trim(),
            AddressLine1         = req.AddressLine1.Trim(),
            AddressLine2         = req.AddressLine2?.Trim(),
            City                 = req.City.Trim(),
            State                = req.State.Trim(),
            PostalCode           = req.PostalCode.Trim(),
            Latitude                  = req.Latitude,
            Longitude                  = req.Longitude,
            IsDefault            = req.IsDefault,
            CreatedAt            = DateTime.UtcNow,
            // UpdatedAt            = DateTime.UtcNow,
        };

        await _customerRepo.AddSavedAddressAsync(address);

        // Single SaveAsync commits both ClearDefault and Add atomically
        await _customerRepo.SaveAsync();

        return MapToAddressDto(address);
    }

    /// <summary>
    /// Updates an existing saved address.
    /// Ownership is enforced at the repository query level —
    /// GetSavedAddressByIdAsync includes customer_id in the WHERE clause.
    ///
    /// If IsDefault = true: clears existing default before applying
    /// the update, all in one SaveAsync call.
    /// </summary>
    public async Task<SavedAddressDto> UpdateSavedAddressAsync(
        int userId,
        int addressId,
        SavedAddressRequestDto req)
    {
        var profile = await _customerRepo.GetByUserIdAsync(userId)
            ?? throw new NotFoundException(
                "Customer profile not found for this account.");

        // Ownership enforced in the query — returns null if not owned
        var address = await _customerRepo.GetSavedAddressByIdAsync(
            addressId, profile.Id)
            ?? throw new NotFoundException(
                $"Saved address {addressId} not found.");

        // Enforce single-default rule if this update sets a new default
        if (req.IsDefault && !address.IsDefault)
            await _customerRepo.ClearDefaultAddressAsync(profile.Id);

        address.Label        = req.Label.Trim();
        address.AddressLine1 = req.AddressLine1.Trim();
        address.AddressLine2 = req.AddressLine2?.Trim();
        address.City         = req.City.Trim();
        address.State        = req.State.Trim();
        address.PostalCode   = req.PostalCode.Trim();
        address.Latitude          = req.Latitude;
        address.Longitude          = req.Longitude;
        address.IsDefault    = req.IsDefault;
        // address.UpdatedAt    = DateTime.UtcNow;

        await _customerRepo.UpdateAddressAsync(address);
        await _customerRepo.SaveAsync();

        return MapToAddressDto(address);
    }

    /// <summary>
    /// Hard-deletes a saved address.
    /// Ownership enforced at query level — returns 404 (not 403) if
    /// address does not exist or does not belong to this customer.
    /// 404 is intentional: we do not reveal that the address exists
    /// and belongs to someone else.
    /// </summary>
    public async Task DeleteSavedAddressAsync(int userId, int addressId)
    {
        var profile = await _customerRepo.GetByUserIdAsync(userId)
            ?? throw new NotFoundException(
                "Customer profile not found for this account.");

        var address = await _customerRepo.GetSavedAddressByIdAsync(
            addressId, profile.Id)
            ?? throw new NotFoundException(
                $"Saved address {addressId} not found.");

        await _customerRepo.DeleteSavedAddressAsync(address);
        await _customerRepo.SaveAsync();
    }

 
    private static CustomerProfileDto MapToProfileDto(CustomerProfile profile) => new()
    {
        UserId               = profile.UserId,
        FullName             = profile.User.FullName,
        Email                = profile.User.Email,
        PhoneNumber          = profile.PhoneNumber,
        AlternatePhoneNumber = profile.AlternatePhoneNumber,
        ProfileImageUrl      = profile.ProfileImageUrl,
        // CreatedAt            = profile.CreatedAt
    };

    private static SavedAddressDto MapToAddressDto(SavedAddress a) => new()
    {
        Id           = a.Id,
        Label        = a.Label,
        AddressLine1 = a.AddressLine1,
        AddressLine2 = a.AddressLine2,
        City         = a.City,
        State        = a.State,
        PostalCode   = a.PostalCode,
        Latitude          = a.Latitude,
        Longitude     = a.Longitude,
        IsDefault    = a.IsDefault,
        // CreatedAt    = a.CreatedAt
    };
}