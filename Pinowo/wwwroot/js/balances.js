// Live balances panel for a group. Loads balances from the REST API and
// re-fetches whenever the SignalR hub signals "BalancesChanged" for this group.
(function () {
    const panel = document.getElementById("balances-panel");
    if (!panel) return;

    const groupId = parseInt(panel.dataset.groupId, 10);
    const currentUserId = panel.dataset.currentUserId;
    const content = document.getElementById("balances-content");
    const status = document.getElementById("balances-status");

    const usd = (n) => "$" + Number(n).toFixed(2);

    function render(data) {
        const positions = data.overallPositions || [];
        const settlements = data.suggestedSettlements || [];

        // Empty = everyone is settled up.
        if (positions.length === 0) {
            content.innerHTML =
                '<div class="alert-success rounded-xl px-4 py-4 flex items-center gap-2">' +
                '<span class="material-symbols-outlined">verified</span>' +
                '<span class="font-medium">All settled up - no outstanding balances.</span></div>';
            return;
        }

        const me = positions.find((p) => String(p.userId) === String(currentUserId));
        const myNet = me ? me.netPositionUsd : 0;
        const heroCls = myNet > 0 ? "text-pos" : (myNet < 0 ? "text-neg" : "text-on-surface");
        const heroSign = myNet > 0 ? "+" : (myNet < 0 ? "−" : "");
        const heroLabel = myNet > 0 ? "you are owed" : (myNet < 0 ? "you owe" : "you are settled up");

        let html = "";

        // Hero figure: the current user's net position.
        html +=
            '<div class="mb-6">' +
            `<div class="font-serif text-4xl md:text-5xl tabular-nums ${heroCls}">${heroSign}${usd(Math.abs(myNet))}</div>` +
            `<div class="field-label !mb-0 mt-1">${heroLabel}</div>` +
            "</div>";

        // Per-member positions.
        html += '<div class="grid sm:grid-cols-2 gap-3 mb-6">';
        for (const p of positions) {
            const owed = p.netPositionUsd >= 0;
            const isMe = String(p.userId) === String(currentUserId);
            const badgeCls = owed ? "badge-pos" : "badge-neg";
            const badgeText = owed
                ? "is owed " + usd(p.netPositionUsd)
                : "owes " + usd(Math.abs(p.netPositionUsd));
            html +=
                '<div class="flex items-center justify-between p-4 rounded-lg hairline" style="background:#fff8f3;">' +
                `<span class="font-medium text-on-surface">${escapeHtml(p.userName)}` +
                (isMe ? ' <span class="text-gold text-xs font-bold">(you)</span>' : "") +
                "</span>" +
                `<span class="chip ${badgeCls}">${badgeText}</span>` +
                "</div>";
        }
        html += "</div>";

        // Suggested settlements.
        if (settlements.length) {
            html += '<div class="h-px bg-hairline mb-5"></div>';
            html += '<h3 class="field-label mb-3">Suggested settlements</h3><div class="space-y-2">';
            for (const s of settlements) {
                html +=
                    '<div class="flex items-center justify-between bg-surface-low rounded-lg px-4 py-3 hairline">' +
                    `<span class="text-on-surface"><strong class="font-medium">${escapeHtml(s.owedByUserName)}</strong> ` +
                    `<span class="text-on-surface-variant">pays</span> ` +
                    `<strong class="font-medium">${escapeHtml(s.owedToUserName)}</strong></span>` +
                    `<span class="pill-gold tabular-nums">${usd(s.netAmountUsd)}</span>` +
                    "</div>";
            }
            html += "</div>";
        }

        content.innerHTML = html;
    }

    function escapeHtml(s) {
        const d = document.createElement("div");
        d.textContent = s ?? "";
        return d.innerHTML;
    }

    async function load() {
        try {
            const res = await fetch(`/api/groups/${groupId}/balances`, {
                headers: { "Accept": "application/json" }
            });
            if (!res.ok) throw new Error("HTTP " + res.status);
            render(await res.json());
        } catch (e) {
            content.innerHTML =
                '<div class="alert-warn rounded-xl px-4 py-3">Could not load balances.</div>';
            console.error("balances load failed", e);
        }
    }

    // 1) Always render balances from the REST API first. This must NOT depend
    //    on SignalR - if the SignalR script failed to load, the panel still works
    //    (it just won't auto-update). Previously a missing signalR reference threw
    //    here and left the panel stuck on "Loading…".
    load();

    // 2) Live updates are an optional enhancement layered on top.
    if (typeof signalR === "undefined") {
        if (status) status.textContent = "offline";
        console.warn("SignalR client unavailable; balances won't auto-update.");
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/balances")
        .withAutomaticReconnect()
        .build();

    connection.on("BalancesChanged", function (changedGroupId) {
        if (changedGroupId === groupId) {
            if (status) status.textContent = "updating…";
            load().finally(() => { if (status) status.textContent = "live"; });
        }
    });

    connection.onreconnected(() => connection.invoke("JoinGroup", groupId).catch(console.error));

    connection.start()
        .then(async function () {
            try {
                await connection.invoke("JoinGroup", groupId);
                if (status) status.textContent = "live";
            } catch (e) {
                if (status) status.textContent = "offline";
                console.error("JoinGroup failed", e);
            }
        })
        .catch(function (e) {
            console.error("SignalR connect failed; live updates disabled", e);
            if (status) status.textContent = "offline";
        });
})();
