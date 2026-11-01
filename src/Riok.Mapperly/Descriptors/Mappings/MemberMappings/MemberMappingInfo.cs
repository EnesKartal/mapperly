using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Riok.Mapperly.Configuration;
using Riok.Mapperly.Symbols.Members;

namespace Riok.Mapperly.Descriptors.Mappings.MemberMappings;

[DebuggerDisplay("{DebuggerDisplay}")]
public record MemberMappingInfo(
    SourceMemberPath? SourceMember,
    NonEmptyMemberPath TargetMember,
    MemberValueMappingConfiguration? ValueConfiguration = null,
    MemberMappingConfiguration? Configuration = null
)
{
    public MemberMappingInfo(SourceMemberPath? sourceMember, NonEmptyMemberPath targetMember, MemberMappingConfiguration? configuration)
        : this(sourceMember, targetMember, null, configuration) { }

    public MemberMappingInfo(NonEmptyMemberPath targetMember, MemberValueMappingConfiguration configuration)
        : this(null, targetMember, configuration) { }

    public bool IsSourceNullable => SourceMember?.MemberPath.IsAnyReadNullable() ?? ValueConfiguration?.Value?.ConstantValue.IsNull ?? true;

    private string DebuggerDisplay =>
        $"{SourceMember?.MemberPath.FullName ?? ValueConfiguration?.DescribeValue()} => {TargetMember.FullName}";

    /// <summary>
    /// Returns <c>true</c> if the <see cref="SourceMember"/> and <see cref="TargetMember"/>
    /// were matched automatically by Mapperly without any additional user configuration.
    /// </summary>
    public bool IsAutoMatch => Configuration == null && ValueConfiguration == null;

    public TypeMappingKey ToTypeMappingKey()
    {
        if (SourceMember == null)
            throw new InvalidOperationException($"{SourceMember} and {TargetMember} need to be set to create a {nameof(TypeMappingKey)}");

        return new TypeMappingKey(
            SourceMember.MemberPath.MemberReadType,
            TargetMember.MemberWriteType,
            Configuration?.ToTypeMappingConfiguration()
        );
    }

    /// <summary>
    /// Returns the <see cref="TypeMappingKey"/> of a mapping onto the existing <see cref="TargetMember"/>.
    /// The nullable parents of the <see cref="SourceMember"/> are null checked before this mapping is called,
    /// therefore only the nullability of the source member itself is relevant.
    /// </summary>
    public TypeMappingKey ToExistingTargetTypeMappingKey()
    {
        if (SourceMember?.MemberPath.Member is not { } sourceMember)
            return ToTypeMappingKey();

        var sourceType = sourceMember.IsReadNullable
            ? sourceMember.Type.WithNullableAnnotation(NullableAnnotation.Annotated)
            : sourceMember.Type;
        return new TypeMappingKey(sourceType, TargetMember.MemberWriteType, Configuration?.ToTypeMappingConfiguration());
    }

    public string DescribeSource()
    {
        return SourceMember?.MemberPath.ToDisplayString(includeMemberType: false) ?? ValueConfiguration?.DescribeValue() ?? string.Empty;
    }
}
