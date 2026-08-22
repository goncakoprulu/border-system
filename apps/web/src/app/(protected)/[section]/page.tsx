import { notFound } from "next/navigation";
import { ManagementSection } from "@/components/operations/management-section";

const sections = ["schedule","attendance","memberships","payments","balances","reports","instructors","users","settings"] as const;
export const dynamicParams = false;
export function generateStaticParams(){ return sections.map((section)=>({section})); }
export default async function Page({params}:{params:Promise<{section:string}>}) { const {section}=await params; if(!sections.includes(section as typeof sections[number])) notFound(); return <ManagementSection section={section as typeof sections[number]} />; }
