using NUnit.Framework;
using Moq;
using ShipmentTrackingAPI.Services;
using ShipmentTrackingAPI.DTOs.Customer;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Models.Exceptions;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;

namespace ShipmentTrackingAPI.Tests.Services;

/// <summary>
/// Full coverage tests for CustomerService.
///
/// Every public method, every branch, every exception path is covered.
/// Uses Moq to mock ICustomerRepository — no DB, no EF Core, no I/O.
///
/// COVERAGE MAP
/// ────────────
/// GetProfileAsync
///   ✓ profile found → returns mapped DTO
///   ✓ profile not found → throws NotFoundException
///
/// UpdateProfileAsync
///   ✓ all fields provided → all fields updated
///   ✓ null fields → existing values preserved (partial update)
///   ✓ profile not found → throws NotFoundException
///   ✓ UpdateAsync + SaveAsync both called
///
/// GetSavedAddressesAsync
///   ✓ returns mapped list ordered correctly
///   ✓ empty address book → returns empty list
///   ✓ profile not found → throws NotFoundException
///
/// AddSavedAddressAsync
///   ✓ non-default address → ClearDefault NOT called
///   ✓ IsDefault = true → ClearDefault called before insert
///   ✓ address fields are trimmed
///   ✓ profile not found → throws NotFoundException
///   ✓ AddSavedAddressAsync + SaveAsync both called
///
/// UpdateSavedAddressAsync
///   ✓ address found → fields updated
///   ✓ IsDefault changes true → ClearDefault called
///   ✓ already IsDefault and req.IsDefault = true → ClearDefault NOT called
///   ✓ address not owned by customer → throws NotFoundException
///   ✓ profile not found → throws NotFoundException
///   ✓ UpdateAddressAsync + SaveAsync both called
///
/// DeleteSavedAddressAsync
///   ✓ address found → deleted + saved
///   ✓ address not owned → throws NotFoundException
///   ✓ profile not found → throws NotFoundException
///   ✓ DeleteSavedAddressAsync + SaveAsync both called
/// </summary>
[TestFixture]
public class CustomerServiceTest
{
    private Mock<ICustomerRepository> _repoMock = null!;
    private CustomerService           _sut      = null!;

    // ── Shared test data ─────────────────────────────────────

    private static CustomerProfile MakeProfile(int userId = 1) => new()
    {
        Id                   = 10,
        UserId               = userId,
        PhoneNumber          = "9876543210",
        AlternatePhoneNumber = "9000000000",
        ProfileImageUrl      = "https://example.com/avatar.png",
        UpdatedAt            = DateTime.UtcNow,
        User = new User
        {
            Id       = userId,
            FullName = "Test Customer",
            Email    = "customer@test.com",
        }
    };

    private static SavedAddress MakeAddress(int id = 1, bool isDefault = false) => new()
    {
        Id           = id,
        CustomerId   = 10,
        Label        = "Home",
        AddressLine1 = "123 Test Street",
        AddressLine2 = "Apt 4B",
        City         = "Hyderabad",
        State        = "Telangana",
        PostalCode   = "500001",
        Latitude     = 17.385,
        Longitude    = 78.487,
        IsDefault    = isDefault,
        CreatedAt    = DateTime.UtcNow,
    };

    private static SavedAddressRequestDto MakeAddressRequest(bool isDefault = false) => new()
    {
        Label        = "  Office  ",
        AddressLine1 = "  456 Work Road  ",
        AddressLine2 = "  Floor 3  ",
        City         = "  Hyderabad  ",
        State        = "  Telangana  ",
        PostalCode   = "  500002  ",
        Latitude     = 17.440,
        Longitude    = 78.498,
        IsDefault    = isDefault,
    };

    // ── Setup ────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        _repoMock = new Mock<ICustomerRepository>(MockBehavior.Strict);
        _sut      = new CustomerService(_repoMock.Object);
    }

    // ═══════════════════════════════════════════════════════════
    //  GetProfileAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task GetProfileAsync_ProfileExists_ReturnsMappedDto()
    {
        // Arrange
        var profile = MakeProfile(userId: 5);
        _repoMock.Setup(r => r.GetByUserIdAsync(5))
                 .ReturnsAsync(profile);

        // Act
        var result = await _sut.GetProfileAsync(5);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.UserId,               Is.EqualTo(5));
            Assert.That(result.FullName,             Is.EqualTo("Test Customer"));
            Assert.That(result.Email,                Is.EqualTo("customer@test.com"));
            Assert.That(result.PhoneNumber,          Is.EqualTo("9876543210"));
            Assert.That(result.AlternatePhoneNumber, Is.EqualTo("9000000000"));
            Assert.That(result.ProfileImageUrl,      Is.EqualTo("https://example.com/avatar.png"));
        });
    }

    [Test]
    public void GetProfileAsync_ProfileNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _repoMock.Setup(r => r.GetByUserIdAsync(99))
                 .ReturnsAsync((CustomerProfile?)null);

        // Act & Assert
        Assert.ThrowsAsync<NotFoundException>(
            () => _sut.GetProfileAsync(99));
    }

    // ═══════════════════════════════════════════════════════════
    //  UpdateProfileAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task UpdateProfileAsync_AllFieldsProvided_UpdatesAllFields()
    {
        // Arrange
        var profile = MakeProfile();
        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.UpdateAsync(profile)).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        var req = new UpdateProfileRequestDto
        {
            PhoneNumber          = "  1111111111  ",
            AlternatePhoneNumber = "  2222222222  ",
            ProfileImageUrl      = "  https://new.com/img.png  ",
        };

        // Act
        var result = await _sut.UpdateProfileAsync(1, req);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(profile.PhoneNumber,          Is.EqualTo("1111111111"));
            Assert.That(profile.AlternatePhoneNumber, Is.EqualTo("2222222222"));
            Assert.That(profile.ProfileImageUrl,      Is.EqualTo("https://new.com/img.png"));
            Assert.That(result.PhoneNumber,           Is.EqualTo("1111111111"));
        });

        _repoMock.Verify(r => r.UpdateAsync(profile), Times.Once);
        _repoMock.Verify(r => r.SaveAsync(),          Times.Once);
    }

    [Test]
    public async Task UpdateProfileAsync_NullFields_PreservesExistingValues()
    {
        // Arrange
        var profile = MakeProfile();
        var originalPhone    = profile.PhoneNumber;
        var originalAltPhone = profile.AlternatePhoneNumber;
        var originalImageUrl = profile.ProfileImageUrl;

        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.UpdateAsync(profile)).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        var req = new UpdateProfileRequestDto
        {
            PhoneNumber          = null,
            AlternatePhoneNumber = null,
            ProfileImageUrl      = null,
        };

        // Act
        await _sut.UpdateProfileAsync(1, req);

        // Assert — original values unchanged
        Assert.Multiple(() =>
        {
            Assert.That(profile.PhoneNumber,          Is.EqualTo(originalPhone));
            Assert.That(profile.AlternatePhoneNumber, Is.EqualTo(originalAltPhone));
            Assert.That(profile.ProfileImageUrl,      Is.EqualTo(originalImageUrl));
        });
    }

    [Test]
    public async Task UpdateProfileAsync_OnlyPhoneProvided_OnlyPhoneUpdated()
    {
        // Arrange
        var profile = MakeProfile();
        var originalAltPhone = profile.AlternatePhoneNumber;

        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.UpdateAsync(profile)).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        var req = new UpdateProfileRequestDto
        {
            PhoneNumber          = "5555555555",
            AlternatePhoneNumber = null,
            ProfileImageUrl      = null,
        };

        // Act
        await _sut.UpdateProfileAsync(1, req);

        // Assert
        Assert.That(profile.PhoneNumber,          Is.EqualTo("5555555555"));
        Assert.That(profile.AlternatePhoneNumber, Is.EqualTo(originalAltPhone));
    }

    [Test]
    public void UpdateProfileAsync_ProfileNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _repoMock.Setup(r => r.GetByUserIdAsync(99))
                 .ReturnsAsync((CustomerProfile?)null);

        var req = new UpdateProfileRequestDto { PhoneNumber = "123" };

        // Act & Assert
        Assert.ThrowsAsync<NotFoundException>(
            () => _sut.UpdateProfileAsync(99, req));
    }

    [Test]
    public async Task UpdateProfileAsync_UpdatesTimestamp()
    {
        // Arrange
        var profile   = MakeProfile();
        var before    = DateTime.UtcNow.AddSeconds(-1);

        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.UpdateAsync(profile)).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateProfileAsync(1, new UpdateProfileRequestDto());

        // Assert
        Assert.That(profile.UpdatedAt, Is.GreaterThanOrEqualTo(before));
    }

    // ═══════════════════════════════════════════════════════════
    //  GetSavedAddressesAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task GetSavedAddressesAsync_AddressesExist_ReturnsMappedList()
    {
        // Arrange
        var profile   = MakeProfile();
        var addresses = new List<SavedAddress>
        {
            MakeAddress(id: 1, isDefault: true),
            MakeAddress(id: 2, isDefault: false),
        };

        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.GetSavedAddressesAsync(profile.Id))
                 .ReturnsAsync(addresses);

        // Act
        var result = await _sut.GetSavedAddressesAsync(1);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Count,          Is.EqualTo(2));
            Assert.That(result[0].Id,          Is.EqualTo(1));
            Assert.That(result[0].IsDefault,   Is.True);
            Assert.That(result[0].Label,       Is.EqualTo("Home"));
            Assert.That(result[0].City,        Is.EqualTo("Hyderabad"));
            Assert.That(result[0].Latitude,    Is.EqualTo(17.385));
            Assert.That(result[0].Longitude,   Is.EqualTo(78.487));
            Assert.That(result[1].IsDefault,   Is.False);
        });
    }

    [Test]
    public async Task GetSavedAddressesAsync_EmptyAddressBook_ReturnsEmptyList()
    {
        // Arrange
        var profile = MakeProfile();
        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.GetSavedAddressesAsync(profile.Id))
                 .ReturnsAsync(new List<SavedAddress>());

        // Act
        var result = await _sut.GetSavedAddressesAsync(1);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GetSavedAddressesAsync_ProfileNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _repoMock.Setup(r => r.GetByUserIdAsync(99))
                 .ReturnsAsync((CustomerProfile?)null);

        // Act & Assert
        Assert.ThrowsAsync<NotFoundException>(
            () => _sut.GetSavedAddressesAsync(99));
    }

    // ═══════════════════════════════════════════════════════════
    //  AddSavedAddressAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task AddSavedAddressAsync_NonDefault_DoesNotClearDefault()
    {
        // Arrange
        var profile = MakeProfile();
        var req     = MakeAddressRequest(isDefault: false);

        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.AddSavedAddressAsync(It.IsAny<SavedAddress>()))
                 .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        // Act
        await _sut.AddSavedAddressAsync(1, req);

        // Assert — ClearDefault must NOT have been called
        _repoMock.Verify(r => r.ClearDefaultAddressAsync(It.IsAny<int>()), Times.Never);
        _repoMock.Verify(r => r.AddSavedAddressAsync(It.IsAny<SavedAddress>()), Times.Once);
        _repoMock.Verify(r => r.SaveAsync(), Times.Once);
    }

    [Test]
    public async Task AddSavedAddressAsync_IsDefault_ClearsExistingDefault()
    {
        // Arrange
        var profile = MakeProfile();
        var req     = MakeAddressRequest(isDefault: true);

        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.ClearDefaultAddressAsync(profile.Id))
                 .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.AddSavedAddressAsync(It.IsAny<SavedAddress>()))
                 .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        // Act
        await _sut.AddSavedAddressAsync(1, req);

        // Assert — ClearDefault called with correct profile ID
        _repoMock.Verify(r => r.ClearDefaultAddressAsync(profile.Id), Times.Once);
        _repoMock.Verify(r => r.AddSavedAddressAsync(It.IsAny<SavedAddress>()), Times.Once);
    }

    [Test]
    public async Task AddSavedAddressAsync_TrimsAllStringFields()
    {
        // Arrange
        var profile  = MakeProfile();
        var req      = MakeAddressRequest(isDefault: false);
        SavedAddress? captured = null;

        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.AddSavedAddressAsync(It.IsAny<SavedAddress>()))
                 .Callback<SavedAddress>(a => captured = a)
                 .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        // Act
        await _sut.AddSavedAddressAsync(1, req);

        // Assert — all string fields are trimmed
        Assert.Multiple(() =>
        {
            Assert.That(captured!.Label,        Is.EqualTo("Office"));
            Assert.That(captured.AddressLine1,  Is.EqualTo("456 Work Road"));
            Assert.That(captured.AddressLine2,  Is.EqualTo("Floor 3"));
            Assert.That(captured.City,          Is.EqualTo("Hyderabad"));
            Assert.That(captured.State,         Is.EqualTo("Telangana"));
            Assert.That(captured.PostalCode,    Is.EqualTo("500002"));
            Assert.That(captured.CustomerId,    Is.EqualTo(profile.Id));
            Assert.That(captured.Latitude,      Is.EqualTo(17.440));
            Assert.That(captured.Longitude,     Is.EqualTo(78.498));
        });
    }

    [Test]
    public async Task AddSavedAddressAsync_NullAddressLine2_SetsNull()
    {
        // Arrange
        var profile = MakeProfile();
        var req     = MakeAddressRequest();
        req.AddressLine2 = null;

        SavedAddress? captured = null;

        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.AddSavedAddressAsync(It.IsAny<SavedAddress>()))
                 .Callback<SavedAddress>(a => captured = a)
                 .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        // Act
        await _sut.AddSavedAddressAsync(1, req);

        // Assert
        Assert.That(captured!.AddressLine2, Is.Null);
    }

    [Test]
    public async Task AddSavedAddressAsync_ReturnsMappedDto()
    {
        // Arrange
        var profile = MakeProfile();
        var req     = MakeAddressRequest(isDefault: true);

        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.ClearDefaultAddressAsync(profile.Id))
                 .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.AddSavedAddressAsync(It.IsAny<SavedAddress>()))
                 .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _sut.AddSavedAddressAsync(1, req);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Label,     Is.EqualTo("Office"));
            Assert.That(result.IsDefault, Is.True);
            Assert.That(result.City,      Is.EqualTo("Hyderabad"));
        });
    }

    [Test]
    public void AddSavedAddressAsync_ProfileNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _repoMock.Setup(r => r.GetByUserIdAsync(99))
                 .ReturnsAsync((CustomerProfile?)null);

        // Act & Assert
        Assert.ThrowsAsync<NotFoundException>(
            () => _sut.AddSavedAddressAsync(99, MakeAddressRequest()));
    }

    // ═══════════════════════════════════════════════════════════
    //  UpdateSavedAddressAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task UpdateSavedAddressAsync_ValidRequest_UpdatesAllFields()
    {
        // Arrange
        var profile = MakeProfile();
        var address = MakeAddress(id: 1, isDefault: false);
        var req     = MakeAddressRequest(isDefault: false);

        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.GetSavedAddressByIdAsync(1, profile.Id))
                 .ReturnsAsync(address);
        _repoMock.Setup(r => r.UpdateAddressAsync(address)).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _sut.UpdateSavedAddressAsync(1, 1, req);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(address.Label,       Is.EqualTo("Office"));
            Assert.That(address.City,        Is.EqualTo("Hyderabad"));
            Assert.That(address.PostalCode,  Is.EqualTo("500002"));
            Assert.That(address.Latitude,    Is.EqualTo(17.440));
            Assert.That(result.Label,        Is.EqualTo("Office"));
        });

        _repoMock.Verify(r => r.UpdateAddressAsync(address), Times.Once);
        _repoMock.Verify(r => r.SaveAsync(),                 Times.Once);
    }

    [Test]
    public async Task UpdateSavedAddressAsync_SetIsDefaultTrue_WhenPreviouslyFalse_ClearsDefault()
    {
        // Arrange
        var profile = MakeProfile();
        var address = MakeAddress(id: 1, isDefault: false);  // was NOT default
        var req     = MakeAddressRequest(isDefault: true);   // request to SET default

        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.GetSavedAddressByIdAsync(1, profile.Id))
                 .ReturnsAsync(address);
        _repoMock.Setup(r => r.ClearDefaultAddressAsync(profile.Id))
                 .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.UpdateAddressAsync(address)).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateSavedAddressAsync(1, 1, req);

        // Assert — clear was called because we're setting a new default
        _repoMock.Verify(r => r.ClearDefaultAddressAsync(profile.Id), Times.Once);
        Assert.That(address.IsDefault, Is.True);
    }

    [Test]
    public async Task UpdateSavedAddressAsync_AlreadyDefault_WhenSetDefaultTrue_DoesNotClearDefault()
    {
        // Arrange
        var profile = MakeProfile();
        var address = MakeAddress(id: 1, isDefault: true);  // already IS default
        var req     = MakeAddressRequest(isDefault: true);  // request keeps it default

        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.GetSavedAddressByIdAsync(1, profile.Id))
                 .ReturnsAsync(address);
        _repoMock.Setup(r => r.UpdateAddressAsync(address)).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateSavedAddressAsync(1, 1, req);

        // Assert — no need to clear because address was already the default
        _repoMock.Verify(r => r.ClearDefaultAddressAsync(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task UpdateSavedAddressAsync_SetIsDefaultFalse_DoesNotClearDefault()
    {
        // Arrange
        var profile = MakeProfile();
        var address = MakeAddress(id: 1, isDefault: true);
        var req     = MakeAddressRequest(isDefault: false);  // removing default

        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.GetSavedAddressByIdAsync(1, profile.Id))
                 .ReturnsAsync(address);
        _repoMock.Setup(r => r.UpdateAddressAsync(address)).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateSavedAddressAsync(1, 1, req);

        // Assert
        _repoMock.Verify(r => r.ClearDefaultAddressAsync(It.IsAny<int>()), Times.Never);
        Assert.That(address.IsDefault, Is.False);
    }

    [Test]
    public void UpdateSavedAddressAsync_AddressNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var profile = MakeProfile();
        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.GetSavedAddressByIdAsync(999, profile.Id))
                 .ReturnsAsync((SavedAddress?)null);

        // Act & Assert
        Assert.ThrowsAsync<NotFoundException>(
            () => _sut.UpdateSavedAddressAsync(1, 999, MakeAddressRequest()));
    }

    [Test]
    public void UpdateSavedAddressAsync_ProfileNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _repoMock.Setup(r => r.GetByUserIdAsync(99))
                 .ReturnsAsync((CustomerProfile?)null);

        // Act & Assert
        Assert.ThrowsAsync<NotFoundException>(
            () => _sut.UpdateSavedAddressAsync(99, 1, MakeAddressRequest()));
    }

    [Test]
    public async Task UpdateSavedAddressAsync_TrimsAllStringFields()
    {
        // Arrange
        var profile = MakeProfile();
        var address = MakeAddress(id: 1);
        var req     = MakeAddressRequest();   // all fields have leading/trailing spaces

        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.GetSavedAddressByIdAsync(1, profile.Id))
                 .ReturnsAsync(address);
        _repoMock.Setup(r => r.UpdateAddressAsync(address)).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateSavedAddressAsync(1, 1, req);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(address.Label,       Is.EqualTo("Office"));
            Assert.That(address.AddressLine1,Is.EqualTo("456 Work Road"));
            Assert.That(address.City,        Is.EqualTo("Hyderabad"));
            Assert.That(address.State,       Is.EqualTo("Telangana"));
            Assert.That(address.PostalCode,  Is.EqualTo("500002"));
        });
    }

    // ═══════════════════════════════════════════════════════════
    //  DeleteSavedAddressAsync
    // ═══════════════════════════════════════════════════════════

    [Test]
    public async Task DeleteSavedAddressAsync_AddressFound_DeletesAndSaves()
    {
        // Arrange
        var profile = MakeProfile();
        var address = MakeAddress(id: 1);

        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.GetSavedAddressByIdAsync(1, profile.Id))
                 .ReturnsAsync(address);
        _repoMock.Setup(r => r.DeleteSavedAddressAsync(address))
                 .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        // Act
        await _sut.DeleteSavedAddressAsync(1, 1);

        // Assert
        _repoMock.Verify(r => r.DeleteSavedAddressAsync(address), Times.Once);
        _repoMock.Verify(r => r.SaveAsync(),                      Times.Once);
    }

    [Test]
    public void DeleteSavedAddressAsync_AddressNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var profile = MakeProfile();
        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.GetSavedAddressByIdAsync(999, profile.Id))
                 .ReturnsAsync((SavedAddress?)null);

        // Act & Assert
        Assert.ThrowsAsync<NotFoundException>(
            () => _sut.DeleteSavedAddressAsync(1, 999));
    }

    [Test]
    public void DeleteSavedAddressAsync_ProfileNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _repoMock.Setup(r => r.GetByUserIdAsync(99))
                 .ReturnsAsync((CustomerProfile?)null);

        // Act & Assert
        Assert.ThrowsAsync<NotFoundException>(
            () => _sut.DeleteSavedAddressAsync(99, 1));
    }

    [Test]
    public async Task DeleteSavedAddressAsync_DeleteCalledBeforeSave()
    {
        // Arrange
        var profile  = MakeProfile();
        var address  = MakeAddress(id: 1);
        var callOrder = new List<string>();

        _repoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(profile);
        _repoMock.Setup(r => r.GetSavedAddressByIdAsync(1, profile.Id))
                 .ReturnsAsync(address);
        _repoMock.Setup(r => r.DeleteSavedAddressAsync(address))
                 .Callback(() => callOrder.Add("delete"))
                 .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SaveAsync())
                 .Callback(() => callOrder.Add("save"))
                 .Returns(Task.CompletedTask);

        // Act
        await _sut.DeleteSavedAddressAsync(1, 1);

        // Assert — delete must precede save
        Assert.That(callOrder, Is.EqualTo(new[] { "delete", "save" }));
    }
}