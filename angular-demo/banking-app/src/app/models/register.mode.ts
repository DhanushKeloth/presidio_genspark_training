export class RegisterModel {
  constructor(
    public userName: string="",
    public name: String="",
    public email: string="",
    public phone: string="",
    public status: string="",
    public dateOfBirth: Date=new Date(),
  ) {}
}
