namespace BallastlaneTechStore.Domain.Enums;

public enum UserRole { SalesRep = 0, Manager = 1, Admin = 2 }

public enum CustomerStatus { Lead = 0, Prospect = 1, Active = 2, Churned = 3 }

public enum ProductCategory
{
    Cpu = 0, Gpu = 1, Ram = 2, Ssd = 3,
    Motherboard = 4, Psu = 5, Case = 6, Cooler = 7,
}

public enum OrderStatus { Draft = 0, Confirmed = 1, Fulfilled = 2, Cancelled = 3 }
