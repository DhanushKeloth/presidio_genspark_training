export class RegisterCustomerModel {
    constructor(
        public fullName: string = "",
        public email: string = "",
        public password: string = "",
        public confirmPassword: string = "",
        // Mapped from your nullable C# properties:
        public phoneNumber: string = "",
        public alternatePhoneNumber: string = ""
    ) {}
}