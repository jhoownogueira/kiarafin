export type HouseholdRole = "Owner" | "Member"

export type HouseholdMember = {
  userId: string
  name: string
  email: string
  role: HouseholdRole
  joinedAtUtc: string
}

export type HouseholdInvitation = {
  id: string
  token: string
  expiresAtUtc: string
  invitedEmail: string | null
}

export type Household = {
  id: string
  name: string
  role: HouseholdRole
}
