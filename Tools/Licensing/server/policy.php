<?php
declare(strict_types=1);

function trialDeadline(int $started, int $now): int
{
    if ($started <= 0 || $started > $now) return 0;
    $until = $started + 7 * 86400;
    return $until > $now ? $until : 0;
}

// Only server-fetched PayPal data may reach this function. Paid plans have NO second trial.
function entitlement(array $subscription, array $transactions, string $plan, string $account,
                     int $priceCents, int $now): int
{
    if (($subscription['plan_id'] ?? '') !== $plan || ($subscription['custom_id'] ?? '') !== $account ||
        !in_array($subscription['status'] ?? '', ['ACTIVE', 'CANCELLED', 'EXPIRED'], true) ||
        !empty($subscription['plan_overridden'])) return 0;
    $start = strtotime($subscription['start_time'] ?? '') ?: 0;
    if ($start <= 0 || $start > $now) return 0;
    $until = 0;
    foreach ($transactions as $tx) {
        $amount = $tx['amount_with_breakdown']['gross_amount'] ?? [];
        if (!preg_match('/^([0-9]+)(?:\.([0-9]{1,2}))?$/D', (string)($amount['value'] ?? ''), $match)) continue;
        $cents = (int)$match[1] * 100 + (int)str_pad($match[2] ?? '', 2, '0');
        $at = strtotime($tx['time'] ?? '') ?: 0;
        if (($tx['status'] ?? '') !== 'COMPLETED' || ($amount['currency_code'] ?? '') !== 'EUR' ||
            $cents !== $priceCents || $at < $start || $at > $now) continue;
        $until = max($until, $at + 30 * 86400);
    }
    return $until > $now ? $until : 0;
}
