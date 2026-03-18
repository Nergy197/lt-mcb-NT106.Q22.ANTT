import mongoose, { Document, Types } from 'mongoose';
export interface IPosition {
    x: number;
    y: number;
    z: number;
}
export interface IPlayer extends Document {
    account_id: Types.ObjectId;
    name: string;
    money: number;
    current_map: string;
    position: IPosition;
    beaten_bosses: string[];
}
export declare const PlayerModel: mongoose.Model<IPlayer, {}, {}, {}, mongoose.Document<unknown, {}, IPlayer, {}, mongoose.DefaultSchemaOptions> & IPlayer & Required<{
    _id: Types.ObjectId;
}> & {
    __v: number;
} & {
    id: string;
}, any, IPlayer>;
//# sourceMappingURL=Player.d.ts.map