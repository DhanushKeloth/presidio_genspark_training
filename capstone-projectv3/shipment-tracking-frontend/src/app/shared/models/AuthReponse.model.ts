export class AuthResponseModel {
    constructor(
        public accessToken: string = "",
        public expiresIn: number = 0,
        public userId: number = 0,
        public fullName: string = "",
        public role: string = ""
    ) {}
}