// Mirrors the .NET DTOs. Hand-written so we don't pull in a generator for ~5 enums.

export type UUID = string;

export type UserRole = "SalesRep" | "Manager" | "Admin";
export type CustomerStatus = "Lead" | "Prospect" | "Active" | "Churned";
export type ProductCategory =
  | "Cpu" | "Gpu" | "Ram" | "Ssd" | "Motherboard" | "Psu" | "Case" | "Cooler";
export type OrderStatus = "Draft" | "Confirmed" | "Fulfilled" | "Cancelled";

export interface UserDto {
  id: UUID; email: string; displayName: string; role: UserRole;
}

export interface AuthResponse {
  token: string; expiresAt: string; user: UserDto;
}

export interface PagedResult<T> {
  items: T[]; total: number; skip: number; take: number;
}

export interface CustomerDto {
  id: UUID; company: string; contactName: string; email: string; phone: string | null;
  status: CustomerStatus; ownerId: UUID; createdAt: string; updatedAt: string;
}

export interface ProductDto {
  id: UUID; sku: string; name: string; category: ProductCategory; brand: string;
  price: number; stockOnHand: number; rowVersion: number; createdAt: string; updatedAt: string;
}

export interface OrderItemDto {
  id: UUID; productId: UUID; productSku: string; productName: string;
  quantity: number; unitPrice: number; lineTotal: number;
}

export interface OrderDto {
  id: UUID; number: string; customerId: UUID; customerCompany: string;
  status: OrderStatus; subtotal: number; tax: number; total: number;
  ownerId: UUID; createdAt: string; updatedAt: string; items: OrderItemDto[];
}

export interface OrderSummaryDto {
  id: UUID; number: string; customerId: UUID; customerCompany: string;
  status: OrderStatus; total: number; createdAt: string;
}

export interface PipelineSummary { status: OrderStatus; count: number; total: number; }

export const customerStatuses: CustomerStatus[] = ["Lead", "Prospect", "Active", "Churned"];
export const productCategories: ProductCategory[] = [
  "Cpu", "Gpu", "Ram", "Ssd", "Motherboard", "Psu", "Case", "Cooler",
];
export const orderStatuses: OrderStatus[] = ["Draft", "Confirmed", "Fulfilled", "Cancelled"];

// Server uses numeric enums on the wire. Encode requests, decode responses.
export const customerStatusToInt: Record<CustomerStatus, number> = { Lead: 0, Prospect: 1, Active: 2, Churned: 3 };
export const productCategoryToInt: Record<ProductCategory, number> = {
  Cpu: 0, Gpu: 1, Ram: 2, Ssd: 3, Motherboard: 4, Psu: 5, Case: 6, Cooler: 7,
};
export const orderStatusToInt: Record<OrderStatus, number> = { Draft: 0, Confirmed: 1, Fulfilled: 2, Cancelled: 3 };
export const userRoleToInt: Record<UserRole, number> = { SalesRep: 0, Manager: 1, Admin: 2 };

export const customerStatusFromInt = invert(customerStatusToInt);
export const productCategoryFromInt = invert(productCategoryToInt);
export const orderStatusFromInt = invert(orderStatusToInt);
export const userRoleFromInt = invert(userRoleToInt);

// Auth payloads can arrive with role as either string ("Manager") or numeric (1).
// Normalise to the string union so downstream code doesn't need to care.
export function normaliseUser<T extends { role: UserRole | number }>(u: T): T & { role: UserRole } {
  return { ...u, role: typeof u.role === "number" ? userRoleFromInt[u.role] : u.role };
}

function invert<K extends string>(m: Record<K, number>): Record<number, K> {
  const out: Record<number, K> = {};
  for (const [k, v] of Object.entries(m)) out[v as number] = k as K;
  return out;
}
