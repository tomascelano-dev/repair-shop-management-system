using System.Buffers.Binary;
using FluentAssertions;
using RepairShop.Application.Files;
using RepairShop.Application.Security;
using RepairShop.Domain.Common;

namespace RepairShop.Application.Tests;

/// <summary>Security audit 2026-11-23: image bombs (M-07) and the password minimum (B-04).</summary>
public sealed class SecurityAuditUnitTests
{
    private static byte[] Png(int width, int height)
    {
        var b = new byte[33];
        new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(b, 0);
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(20), height);
        return b;
    }

    private static byte[] Jpeg(int width, int height)
    {
        var b = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0, 0, 0, 0, 0x03, 0, 0, 0, 0, 0, 0 };
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(13), (ushort)height);
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(15), (ushort)width);
        return b;
    }

    [Fact]
    public void M07_image_size_is_read_from_the_header()
    {
        ImageDimensions.TryRead(Png(1200, 800)).Should().Be((1200, 800));
        ImageDimensions.TryRead(Jpeg(4032, 3024)).Should().Be((4032, 3024));
        ImageDimensions.IsAcceptable(Jpeg(8160, 6120)).Should().BeTrue("a 50 MP phone photo is fine");
        ImageDimensions.IsAcceptable(Png(20000, 20000)).Should().BeFalse("a 400 MP picture would cost gigabytes in every PDF");
        ImageDimensions.IsAcceptable(Jpeg(30000, 100)).Should().BeFalse();
    }

    [Fact]
    public void B04_the_policy_and_the_hasher_agree_on_ten_characters()
    {
        PasswordPolicy.MinLength.Should().Be(10);
        FluentActions.Invoking(() => PasswordPolicy.Validate("Clave1234")).Should().Throw<DomainException>("9 characters used to pass here and crash the hasher");
        FluentActions.Invoking(() => PasswordPolicy.Validate("Clave12345")).Should().NotThrow();
    }
}
