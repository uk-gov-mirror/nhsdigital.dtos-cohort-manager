export async function checkAccess(workgroups_codes: string[]) {
  const rbacCodes = (process.env.COHORT_MANAGER_RBAC_CODE || "")
    .split(",")
    .map((code) => code.trim())
    .filter((code) => code.length > 0);
  return rbacCodes.some((code) => workgroups_codes.includes(code));
}

export async function checkAccessToRemoveDummyGpCode(workgroups_codes: string[]) {
  const rbacCodes = (process.env.COHORT_MANAGER_REMOVE_DUMMY_GP_CODE_RBAC_CODE || "")
    .split(",")
    .map((code) => code.trim())
    .filter((code) => code.length > 0);
  return rbacCodes.some((code) => workgroups_codes.includes(code));
}
