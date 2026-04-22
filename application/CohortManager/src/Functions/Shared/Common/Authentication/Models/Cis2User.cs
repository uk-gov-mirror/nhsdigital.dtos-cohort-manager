namespace Common;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public class Cis2User
{
    [JsonPropertyName("nhsid_useruid")]
    public required string NhsidUseruid { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("nhsid_nrbac_roles")]
    public required List<NhsidNrbacRole> NhsidNrbacRoles { get; set; }

    [JsonPropertyName("given_name")]
    public required string GivenName { get; set; }

    [JsonPropertyName("family_name")]
    public required string FamilyName { get; set; }

    [JsonPropertyName("uid")]
    public required string Uid { get; set; }

    [JsonPropertyName("sub")]
    public required string Sub { get; set; }
}
public class NhsidNrbacRole
{
    [JsonPropertyName("person_orgid")]
    public required string PersonOrgid { get; set; }

    [JsonPropertyName("person_roleid")]
    public required string PersonRoleid { get; set; }

    [JsonPropertyName("org_code")]
    public required string OrgCode { get; set; }

    [JsonPropertyName("role_name")]
    public required string RoleName { get; set; }

    [JsonPropertyName("role_code")]
    public required string RoleCode { get; set; }

    [JsonPropertyName("workgroups")]
    public required List<string> Workgroups { get; set; }

    [JsonPropertyName("workgroups_codes")]
    public required List<string> WorkgroupsCodes { get; set; }
}
