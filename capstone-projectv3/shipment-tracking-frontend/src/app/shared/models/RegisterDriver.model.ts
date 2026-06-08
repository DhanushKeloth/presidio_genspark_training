export class RegisterDriverModel {
    constructor(
        public fullName: string = "",
        public email: string = "",
        public password: string = "",
        public confirmPassword: string = "",
        public phoneNumber: string = "",
        public vehicleType: string = "",
        public vehicleNumber: string = "",
        public licenseNumber: string = ""
    ) {}
}