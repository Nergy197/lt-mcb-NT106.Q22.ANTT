import mongoose, { Document } from 'mongoose';
export interface IAccount extends Document {
    username: string;
    password_hash: string;
    email: string;
    created_at: Date;
}
export declare const AccountModel: mongoose.Model<IAccount, {}, {}, {}, mongoose.Document<unknown, {}, IAccount, {}, mongoose.DefaultSchemaOptions> & IAccount & Required<{
    _id: mongoose.Types.ObjectId;
}> & {
    __v: number;
} & {
    id: string;
}, any, IAccount>;
//# sourceMappingURL=Account.d.ts.map